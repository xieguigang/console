Imports System.Text

''' <summary>
''' 将包含 ANSI escape sequence（xterm 子集）的文本渲染到 RichTextBox 上，
''' 支持 SGR 颜色/样式（含 256 色与真彩色、亮色）、回车重绘(\r)、退格(\b)、
''' 行内擦除(K) 与屏幕擦除(J)。渲染状态以 <see cref="AnsiTerminalState"/> 实例为单位维护，
''' 可跨多次调用延续格式（需配合 ConsoleControl 的分片缓冲，避免转义序列被截断）。
''' </summary>
Public Class AnsiEscapeRenderer

    Private Shared ReadOnly EscapeChar As Char = ChrW(&H1B) ' ESC 字符 (0x1B)
    Private Shared ReadOnly OscChar As Char = ChrW(&H5D)    ' OSC 字符 (0x9D) - 忽略
    Private Const CSI As String = "["c                      ' CSI 引入符

    ' 标准 16 色（0-7 普通 / 8-15 亮色）：索引 = code - 30（前景）或 code - 40（背景）
    ' Public so the grid-based terminal renderer (TerminalControl) can reuse the
    ' exact same palette.
    Public Shared ReadOnly StandardColors As Color() = {
        Color.Black, Color.DarkRed, Color.DarkGreen, Color.DarkOrange,
        Color.DarkBlue, Color.DarkMagenta, Color.DarkCyan, Color.Gray,
        Color.DarkGray, Color.Red, Color.Green, Color.Yellow,
        Color.Blue, Color.Magenta, Color.Cyan, Color.White
    }

    ''' <summary>
    ''' 跨调用延续的终端格式状态。默认与 ConsoleControl 的 RichTextBox 黑底白字一致。
    ''' </summary>
    Public Class AnsiTerminalState
        Public ForeColor As Color = Color.White
        Public BackColor As Color = Color.Black
        Public Style As FontStyle = FontStyle.Regular

        ''' <summary>
        ''' 重置为默认（白字黑底、常规样式）。对应 SGR 0 / 39 / 49。
        ''' </summary>
        Public Sub Reset()
            ForeColor = Color.White
            BackColor = Color.Black
            Style = FontStyle.Regular
        End Sub
    End Class

    ''' <summary>
    ''' 将 ANSI 文本渲染到 RichTextBox（使用调用方持有的状态，跨调用延续格式）。
    ''' </summary>
    ''' <param name="rtb">目标 RichTextBox（应为已创建句柄、且由 UI 线程调用）</param>
    ''' <param name="ansiText">完整（未被截断的）ANSI 文本</param>
    ''' <param name="state">跨调用延续的格式状态；每次调用会读取并写回当前格式</param>
    Public Shared Sub Render(rtb As RichTextBox, ansiText As String, state As AnsiTerminalState)
        If rtb Is Nothing OrElse ansiText Is Nothing Then Return
        If state Is Nothing Then state = New AnsiTerminalState()

        ' 注意：此处不挂起绘制（WM_SETREDRAW）。RichTextBox 在绘制挂起期间设置的
        ' SelectionColor/SelectionFont 等字符格式可能无法持久化，导致样式静默丢失。
        ParseAndApply(rtb, ansiText, state)

        ' 保持光标在文末，避免后续追加错位
        rtb.SelectionStart = rtb.TextLength
        rtb.SelectionLength = 0
    End Sub

    ''' <summary>
    ''' 便捷静态入口：每次调用使用全新状态（不延续格式），用于演示/一次性渲染。
    ''' </summary>
    Public Shared Sub RenderAnsiText(rtb As RichTextBox, ansiText As String)
        Render(rtb, ansiText, New AnsiTerminalState())
    End Sub

    ' ============ 内部解析 ============

    Private Shared Sub ParseAndApply(rtb As RichTextBox, text As String, state As AnsiTerminalState)
        Dim pos As Integer = 0
        Dim buf As New StringBuilder() ' 当前待写入的纯文本缓存（带当前格式）

        Dim Flush = Sub()
                       If buf.Length > 0 Then
                           AppendStyled(rtb, buf.ToString(), state)
                           buf.Clear()
                       End If
                   End Sub

        While pos < text.Length
            Dim ch As Char = text(pos)

            ' 转义序列起始：ESC [ 或 ESC ] （OSC 直接忽略到终止符）
            If ch = EscapeChar Then
                If pos + 1 < text.Length AndAlso text(pos + 1) = "["c Then
                    Flush()
                    Dim seqEnd As Integer = IndexOfCsiEnd(text, pos + 2)
                    If seqEnd < 0 Then
                        ' 序列未结束（理论上不应发生，因为 ConsoleControl 已做缓冲拼接）
                        Exit While
                    End If
                    ' body 需包含终止符（如 'm'），HandleCsi 以 body 末字符判定序列类型
                    Dim body As String = text.Substring(pos + 2, seqEnd - (pos + 2) + 1)
                    HandleCsi(rtb, body, state)
                    pos = seqEnd + 1
                    Continue While
                ElseIf pos + 1 < text.Length AndAlso text(pos + 1) = OscChar Then
                    ' OSC 序列（如设置标题 \e]0;...\a），忽略到 BEL(0x07) 或 ST(\e\)
                    Flush()
                    Dim endOsc As Integer = text.IndexOf(ChrW(&H7), pos + 2)
                    If endOsc < 0 Then Exit While
                    pos = endOsc + 1
                    Continue While
                Else
                    ' 裸 ESC 或其他，跳过
                    pos += 1
                    Continue While
                End If
            End If

            Select Case ch
                Case ChrW(&HD) ' 回车 CR：清除当前行行尾并重绘
                    Flush()
                    CarriageReturn(rtb)
                    pos += 1
                Case ChrW(&H8) ' 退格 BS：删除光标前一个字符
                    Flush()
                    Backspace(rtb)
                    pos += 1
                Case Else
                    buf.Append(ch)
                    pos += 1
            End Select
        End While

        Flush()
    End Sub

    ''' <summary>
    ''' 找到 CSI 序列的结束位置（以字母结尾）。找不到返回 -1。
    ''' </summary>
    Private Shared Function IndexOfCsiEnd(text As String, start As Integer) As Integer
        Dim i As Integer = start
        While i < text.Length
            Dim c As Char = text(i)
            ' 结束符：终止字节（@ 到 ~ 的区间，即 0x40-0x7E 中的字母与符号）
            If (c >= "A"c AndAlso c <= "Z"c) OrElse (c >= "a"c AndAlso c <= "z"c) OrElse c = "~"c Then
                Return i
            End If
            If c = EscapeChar Then Return -1 ' 序列被新的 ESC 打断，视为不完整
            i += 1
        End While
        Return -1
    End Function

    ' ============ CSI 处理 ============

    Private Shared Sub HandleCsi(rtb As RichTextBox, body As String, state As AnsiTerminalState)
        If body.Length = 0 Then Return
        Dim finalChar As Char = body(body.Length - 1)
        Dim paramStr As String = body.Substring(0, body.Length - 1)

        Select Case finalChar
            Case "m"c ' SGR 颜色/样式
                ApplySgr(state, paramStr)
            Case "K"c ' 行内擦除
                EraseInLine(rtb, ParseIntParam(paramStr, 0))
            Case "J"c ' 屏幕擦除
                EraseInDisplay(rtb, ParseIntParam(paramStr, 0))
            Case "H"c, "f"c ' 光标定位（home/定位）—— RichTextBox 无真实网格，作 no-op 以不破坏内容
                ' 故意忽略：避免将光标移动到缓冲区中部导致后续内容被插入到历史文本中
            Case "A"c, "B"c, "C"c, "D"c, "E"c, "F"c, "G"c ' 光标相对移动 —— no-op
            Case "s"c ' 保存光标位置 —— no-op
            Case "u"c ' 恢复光标位置 —— no-op
            Case "h"c, "l"c ' 模式设置/复位 —— 忽略
            Case "r"c ' 滚动区域 —— 忽略
            Case Else
                ' 其它未识别 CSI（如设备状态报告）一律安全忽略
        End Select
    End Sub

    ' ============ SGR ============

    Public Shared Sub ApplySgr(state As AnsiTerminalState, paramStr As String)
        Dim parts As String() = If(String.IsNullOrEmpty(paramStr), New String() {""}, paramStr.Split(";"c))
        Dim i As Integer = 0
        If parts.Length = 1 AndAlso parts(0) = "" Then
            ' 空参数等价于重置
            state.Reset()
            Return
        End If

        While i < parts.Length
            Dim code As Integer = 0
            If Not Integer.TryParse(parts(i), code) Then code = 0

            Select Case code
                Case 0
                    state.Reset()
                Case 1 ' 粗体
                    state.Style = state.Style Or FontStyle.Bold
                Case 2 ' 弱化
                    state.Style = state.Style Or FontStyle.Regular ' RTB 无弱化，近似不处理
                Case 3 ' 斜体
                    state.Style = state.Style Or FontStyle.Italic
                Case 4 ' 下划线
                    state.Style = state.Style Or FontStyle.Underline
                Case 5, 6 ' 闪烁 —— RTB 不支持，忽略
                Case 7 ' 反显：前景/背景互换
                    Dim tmp As Color = state.ForeColor
                    state.ForeColor = state.BackColor
                    state.BackColor = tmp
                Case 8 ' 隐藏 —— RTB 无隐藏属性，近似用背景色覆盖（与背景同色）
                    state.ForeColor = state.BackColor
                Case 9 ' 删除线
                    state.Style = state.Style Or FontStyle.Strikeout
                Case 21 ' 双下划线/关闭粗体近似
                    state.Style = state.Style And Not FontStyle.Bold
                Case 22 ' 关闭粗体/弱化
                    state.Style = state.Style And Not FontStyle.Bold
                Case 23
                    state.Style = state.Style And Not FontStyle.Italic
                Case 24
                    state.Style = state.Style And Not FontStyle.Underline
                Case 27 ' 关闭反显
                    ' 无法可靠撤销，近似重置为默认
                    state.ForeColor = Color.White
                    state.BackColor = Color.Black
                Case 29
                    state.Style = state.Style And Not FontStyle.Strikeout
                Case 30 To 37 ' 标准前景
                    state.ForeColor = StandardColors(code - 30)
                Case 38 ' 扩展前景：38;5;n 或 38;2;r;g;b
                    i = ApplyExtendedColor(state, parts, i, True)
                Case 39 ' 默认前景
                    state.ForeColor = Color.White
                Case 40 To 47 ' 标准背景
                    state.BackColor = StandardColors(code - 40)
                Case 48 ' 扩展背景：48;5;n 或 48;2;r;g;b
                    i = ApplyExtendedColor(state, parts, i, False)
                Case 49 ' 默认背景
                    state.BackColor = Color.Black
                Case 90 To 97 ' 亮色前景（同 8-15）
                    state.ForeColor = StandardColors(code - 90 + 8)
                Case 100 To 107 ' 亮色背景
                    state.BackColor = StandardColors(code - 100 + 8)
            End Select

            i += 1
        End While
    End Sub

    ''' <summary>
    ''' 处理 38 / 48 的扩展颜色（256 色或真彩色）。返回处理到的参数索引。
    ''' </summary>
    Public Shared Function ApplyExtendedColor(state As AnsiTerminalState, parts As String(), i As Integer, isFore As Boolean) As Integer
        Dim idx As Integer = i + 1
        If idx >= parts.Length Then Return i
        Dim mode As Integer = 0
        If Not Integer.TryParse(parts(idx), mode) Then Return i

        If mode = 5 Then
            ' 256 色：38;5;n
            idx += 1
            If idx < parts.Length Then
                Dim n As Integer = 0
                If Integer.TryParse(parts(idx), n) Then
                    Dim c As Color = Xterm256Color(n)
                    If isFore Then state.ForeColor = c Else state.BackColor = c
                End If
            End If
            Return idx
        ElseIf mode = 2 Then
            ' 真彩色：38;2;r;g;b
            idx += 1
            If idx + 2 < parts.Length Then
                Dim r As Integer = 0, g As Integer = 0, b As Integer = 0
                Integer.TryParse(parts(idx), r)
                Integer.TryParse(parts(idx + 1), g)
                Integer.TryParse(parts(idx + 2), b)
                Dim c As Color = Color.FromArgb(ClampByte(r), ClampByte(g), ClampByte(b))
                If isFore Then state.ForeColor = c Else state.BackColor = c
                Return idx + 2
            End If
            Return idx + 2
        End If
        Return idx
    End Function

    Private Shared Function ClampByte(v As Integer) As Integer
        If v < 0 Then Return 0
        If v > 255 Then Return 255
        Return v
    End Function

    ''' <summary>
    ''' 由 xterm 256 调色板索引得到 Color。
    ''' </summary>
    Public Shared Function Xterm256Color(index As Integer) As Color
        If index < 0 Then Return Color.White
        If index < 16 Then
            Return StandardColors(index)
        ElseIf index < 232 Then
            Dim n As Integer = index - 16
            Dim r As Integer = n \ 36
            Dim g As Integer = (n \ 6) Mod 6
            Dim b As Integer = n Mod 6
            Dim levels As Integer() = {0, 95, 135, 175, 215, 255}
            Return Color.FromArgb(levels(r), levels(g), levels(b))
        Else
            Dim v As Integer = 8 + (index - 232) * 10
            Return Color.FromArgb(v, v, v)
        End If
    End Function

    ' ============ 文本写入与光标/擦除操作 ============

    ''' <summary>
    ''' 以当前状态格式追加文本到底部。
    ''' </summary>
    Private Shared Sub AppendStyled(rtb As RichTextBox, text As String, state As AnsiTerminalState)
        If String.IsNullOrEmpty(text) Then Return
        Dim startPos As Integer = rtb.TextLength
        rtb.AppendText(text)
        rtb.Select(startPos, text.Length)

        rtb.SelectionColor = state.ForeColor
        rtb.SelectionBackColor = state.BackColor

        ' RichTextBox 的 SelectionFont 在选区包含多种字体时返回 null，直接赋新字体会被静默忽略。
        ' 因此以当前选区真实字体（null 时回退控件字体）为基准，再叠加目标 Style 构造新字体，
        ' 确保 Bold/Italic/Underline/Strikeout 等样式被可靠应用。
        Dim baseFont As Font = If(rtb.SelectionFont, rtb.Font)
        rtb.SelectionFont = New Font(baseFont.FontFamily, baseFont.Size, state.Style)

        rtb.SelectionStart = rtb.TextLength
        rtb.SelectionLength = 0
    End Sub

    ''' <summary>
    ''' 回车：将光标移回当前行首。终端语义中裸 \r 只重定位光标，不删除任何文本。
    ''' 后续文本从行首覆盖写入；本实现以追加方式保留已有内容，
    ''' 避免在无后续覆盖文本（如进度条未重绘）时丢失已显示内容。
    ''' </summary>
    Private Shared Sub CarriageReturn(rtb As RichTextBox)
        Dim caret As Integer = rtb.TextLength
        Dim lineIdx As Integer = rtb.GetLineFromCharIndex(caret)
        If lineIdx < 0 Then Return
        Dim lineStart As Integer = rtb.GetFirstCharIndexFromLine(lineIdx)
        rtb.SelectionStart = lineStart
        rtb.SelectionLength = 0
    End Sub

    ''' <summary>
    ''' 退格：删除光标前的一个字符。
    ''' </summary>
    Private Shared Sub Backspace(rtb As RichTextBox)
        If rtb.TextLength > 0 Then
            rtb.Select(rtb.TextLength - 1, 1)
            rtb.SelectedText = ""
        End If
        rtb.SelectionStart = rtb.TextLength
        rtb.SelectionLength = 0
    End Sub

    ''' <summary>
    ''' 行内擦除 K：0=光标到行尾，1=行首到光标，2=整行。
    ''' </summary>
    Private Shared Sub EraseInLine(rtb As RichTextBox, mode As Integer)
        Dim caret As Integer = rtb.TextLength
        Dim lineIdx As Integer = rtb.GetLineFromCharIndex(caret)
        If lineIdx < 0 Then Return
        Dim lineStart As Integer = rtb.GetFirstCharIndexFromLine(lineIdx)
        Dim lineEnd As Integer = LineEndIndex(rtb, lineIdx)

        Select Case mode
            Case 0 ' 光标到行尾
                If caret < lineEnd Then DeleteRange(rtb, caret, lineEnd - caret)
            Case 1 ' 行首到光标
                If lineStart < caret Then DeleteRange(rtb, lineStart, caret - lineStart)
            Case 2 ' 整行
                If lineEnd > lineStart Then DeleteRange(rtb, lineStart, lineEnd - lineStart)
        End Select
        rtb.SelectionStart = rtb.TextLength
        rtb.SelectionLength = 0
    End Sub

    ''' <summary>
    ''' 屏幕擦除 J：0=光标到文末，1=文首到光标，2/3=全部。
    ''' 在单缓冲区模型中以“删除文本”近似（保留可见内容一致性，不破坏历史文本结构）。
    ''' </summary>
    Private Shared Sub EraseInDisplay(rtb As RichTextBox, mode As Integer)
        Select Case mode
            Case 0 ' 光标到文末
                If rtb.TextLength > rtb.SelectionStart Then
                    DeleteRange(rtb, rtb.SelectionStart, rtb.TextLength - rtb.SelectionStart)
                End If
            Case 1 ' 文首到光标
                If rtb.SelectionStart > 0 Then
                    DeleteRange(rtb, 0, rtb.SelectionStart)
                End If
            Case 2, 3 ' 全部
                rtb.Select(0, rtb.TextLength)
                rtb.SelectedText = ""
        End Select
        rtb.SelectionStart = rtb.TextLength
        rtb.SelectionLength = 0
    End Sub

    Private Shared Sub DeleteRange(rtb As RichTextBox, start As Integer, len As Integer)
        rtb.Select(start, len)
        rtb.SelectedText = ""
    End Sub

    ''' <summary>
    ''' 获取某行的结束字符索引（不含换行符）。
    ''' </summary>
    Private Shared Function LineEndIndex(rtb As RichTextBox, lineIdx As Integer) As Integer
        If lineIdx + 1 < rtb.Lines.Length Then
            Return rtb.GetFirstCharIndexFromLine(lineIdx + 1) - 1
        End If
        Return rtb.TextLength
    End Function

    Private Shared Function ParseIntParam(paramStr As String, defaultValue As Integer) As Integer
        Dim v As Integer = defaultValue
        If String.IsNullOrEmpty(paramStr) Then Return defaultValue
        Dim first As String = paramStr.Split(";"c)(0)
        If Not Integer.TryParse(first, v) Then Return defaultValue
        Return v
    End Function

End Class

' 文本段数据结构（保留以供外部兼容）
Public Class TextSegment
    Public Property Text As String
    Public Property ForeColor As Color
    Public Property BackColor As Color
    Public Property Style As FontStyle
End Class
