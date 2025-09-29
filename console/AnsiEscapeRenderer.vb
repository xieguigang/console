Imports System.Text

''' <summary>
''' Module code for render the ansi escape sequence text onto richtextbox control with styles.
''' </summary>
Public Class AnsiEscapeRenderer

    Private Shared ReadOnly EscapeChar As Char = ChrW(&H1B) ' ESC字符
    Private Shared currentForeColor As Color = Color.Black
    Private Shared currentBackColor As Color = Color.White
    Private Shared currentStyle As FontStyle = FontStyle.Regular

    ' ANSI颜色代码到Color的映射
    Private Shared ReadOnly AnsiColorMap As New Dictionary(Of Integer, Color) From {
        {30, Color.Black},      ' 黑色前景
        {31, Color.Red},        ' 红色前景
        {32, Color.Green},      ' 绿色前景
        {33, Color.Yellow},     ' 黄色前景
        {34, Color.Blue},       ' 蓝色前景
        {35, Color.Magenta},    ' 洋红前景
        {36, Color.Cyan},       ' 青色前景
        {37, Color.White},      ' 白色前景
        {39, Color.Black},      ' 默认前景（黑色）        
        {40, Color.Black},      ' 黑色背景
        {41, Color.Red},        ' 红色背景
        {42, Color.Green},      ' 绿色背景
        {43, Color.Yellow},     ' 黄色背景
        {44, Color.Blue},       ' 蓝色背景
        {45, Color.Magenta},    ' 洋红背景
        {46, Color.Cyan},       ' 青色背景
        {47, Color.White},      ' 白色背景
        {49, Color.White}       ' 默认背景（白色）
    }

    ' 样式代码映射
    Private Shared ReadOnly StyleMap As New Dictionary(Of Integer, FontStyle) From {
        {0, FontStyle.Regular},   ' 重置/正常
        {1, FontStyle.Bold},      ' 粗体
        {3, FontStyle.Italic},    ' 斜体
        {4, FontStyle.Underline}  ' 下划线
    }

    ''' <summary>
    ''' 将包含ANSI转义序列的文本渲染到RichTextBox
    ''' </summary>
    ''' <param name="rtb">目标RichTextBox控件</param>
    ''' <param name="ansiText">包含ANSI序列的文本</param>
    Public Shared Sub RenderAnsiText(rtb As RichTextBox, ansiText As String)
        ' 保存原始选择状态
        Dim originalStart As Integer = rtb.SelectionStart
        Dim originalLength As Integer = rtb.SelectionLength

        ' 挂起UI更新以提高性能
        SendMessage(rtb.Handle, WM_SETREDRAW, False, 0)

        Try
            ' 清除现有格式状态
            ResetFormatState()

            ' 将文本追加到RichTextBox（先清空或保留原有内容根据需求决定）
            rtb.AppendText("")

            ' 解析并应用ANSI序列
            ParseAndApplyAnsiCodes(rtb, ansiText)
        Finally
            ' 恢复UI更新
            SendMessage(rtb.Handle, WM_SETREDRAW, True, 0)
            rtb.Invalidate()

            ' 恢复原始选择状态
            rtb.SelectionStart = originalStart
            rtb.SelectionLength = originalLength
        End Try
    End Sub

    ''' <summary>
    ''' 解析ANSI序列并应用到RichTextBox
    ''' </summary>
    Private Shared Sub ParseAndApplyAnsiCodes(rtb As RichTextBox, text As String)
        Dim segments As New List(Of TextSegment)()
        Dim currentPos As Integer = 0
        Dim textStart As Integer = 0
        Dim inEscapeSequence As Boolean = False
        Dim escapeBuilder As New StringBuilder()

        ' 解析文本，识别ANSI序列
        While currentPos < text.Length
            Dim currentChar As Char = text(currentPos)

            If inEscapeSequence Then
                escapeBuilder.Append(currentChar)

                ' 检查序列结束（以字母结尾的序列）
                If Char.IsLetter(currentChar) Then
                    inEscapeSequence = False
                    Dim escapeSeq As String = escapeBuilder.ToString()

                    ' 处理转义序列
                    If escapeSeq.StartsWith("[") Then
                        ProcessAnsiSequence(rtb, escapeSeq, segments)
                    End If

                    escapeBuilder.Clear()
                    textStart = currentPos + 1
                End If
            Else
                If currentChar = EscapeChar AndAlso currentPos + 1 < text.Length AndAlso text(currentPos + 1) = "["c Then
                    ' 找到转义序列开始，先保存前面的文本
                    If currentPos > textStart Then
                        segments.Add(New TextSegment With {
                            .Text = text.Substring(textStart, currentPos - textStart),
                            .ForeColor = currentForeColor,
                            .BackColor = currentBackColor,
                            .Style = currentStyle
                        })
                    End If

                    inEscapeSequence = True
                    escapeBuilder.Append("[")
                    currentPos += 1 ' 跳过下一个字符（已经是"["）
                    textStart = currentPos + 1
                End If
            End If

            currentPos += 1
        End While

        ' 添加最后一段文本
        If textStart < text.Length Then
            segments.Add(New TextSegment With {
                .Text = text.Substring(textStart),
                .ForeColor = currentForeColor,
                .BackColor = currentBackColor,
                .Style = currentStyle
            })
        End If

        ' 应用所有文本段落到RichTextBox
        ApplySegmentsToRichTextBox(rtb, segments)
    End Sub

    ''' <summary>
    ''' 处理ANSI控制序列并更新当前格式状态
    ''' </summary>
    Private Shared Sub ProcessAnsiSequence(rtb As RichTextBox, escapeSeq As String, segments As List(Of TextSegment))
        If escapeSeq.Length < 2 Then Return

        ' 提取数字参数（如"31m"中的31）
        Dim codePart As String = escapeSeq.Substring(1, escapeSeq.Length - 1)

        ' 处理SGR（Select Graphic Rendition）命令
        If codePart.EndsWith("m") Then
            Dim codeStr As String = codePart.Substring(0, codePart.Length - 1)
            Dim codes As Integer() = ParseAnsiCodes(codeStr)

            For Each code As Integer In codes
                ApplyAnsiCode(code)
            Next
        End If
    End Sub

    ''' <summary>
    ''' 解析ANSI代码参数（支持分号分隔的多个代码）
    ''' </summary>
    Private Shared Function ParseAnsiCodes(codeStr As String) As Integer()
        If String.IsNullOrEmpty(codeStr) Then Return New Integer() {0} ' 默认重置

        Dim parts As String() = codeStr.Split(";"c)
        Dim codes As New List(Of Integer)()
        Dim code As Integer = Nothing

        For Each part As String In parts
            If Integer.TryParse(part, code) Then
                codes.Add(code)
            Else
                codes.Add(0) ' 解析失败时使用默认值
            End If
        Next

        Return codes.ToArray()
    End Function

    ''' <summary>
    ''' 应用单个ANSI代码到当前格式状态
    ''' </summary>
    Private Shared Sub ApplyAnsiCode(code As Integer)
        ' 处理重置和样式代码
        If StyleMap.ContainsKey(code) Then
            If code = 0 Then
                ' 重置所有属性
                currentForeColor = Color.Black
                currentBackColor = Color.White
                currentStyle = FontStyle.Regular
            Else
                currentStyle = currentStyle Or StyleMap(code)
            End If
        ElseIf AnsiColorMap.ContainsKey(code) Then
            ' 处理颜色代码
            If code >= 30 AndAlso code <= 37 Then
                currentForeColor = AnsiColorMap(code) ' 前景色
            ElseIf code = 39 Then
                currentForeColor = Color.Black ' 默认前景色
            ElseIf code >= 40 AndAlso code <= 47 Then
                currentBackColor = AnsiColorMap(code) ' 背景色
            ElseIf code = 49 Then
                currentBackColor = Color.White ' 默认背景色
            End If
        End If
    End Sub

    ''' <summary>
    ''' 将解析后的文本段落应用到RichTextBox
    ''' </summary>
    Private Shared Sub ApplySegmentsToRichTextBox(rtb As RichTextBox, segments As List(Of TextSegment))
        For Each segment As TextSegment In segments
            If Not String.IsNullOrEmpty(segment.Text) Then
                ' 保存当前插入位置
                Dim startPos As Integer = rtb.TextLength

                ' 追加文本
                rtb.AppendText(segment.Text)

                ' 应用格式
                rtb.Select(startPos, segment.Text.Length)
                rtb.SelectionColor = segment.ForeColor

                ' 创建字体（注意：RichTextBox不支持单独设置背景色）
                Dim currentFont As Font = rtb.SelectionFont
                If currentFont IsNot Nothing Then
                    rtb.SelectionFont = New Font(currentFont.FontFamily, currentFont.Size, segment.Style)
                End If

                ' 取消选择
                rtb.Select(rtb.TextLength, 0)
            End If
        Next
    End Sub

    ''' <summary>
    ''' 重置格式状态到默认值
    ''' </summary>
    Private Shared Sub ResetFormatState()
        currentForeColor = Color.Black
        currentBackColor = Color.White
        currentStyle = FontStyle.Regular
    End Sub

    ' Windows API声明（用于挂起UI更新）
    Private Const WM_SETREDRAW As Integer = &HB

    <System.Runtime.InteropServices.DllImport("user32.dll")>
    Private Shared Function SendMessage(hWnd As IntPtr, msg As Integer, wParam As Boolean, lParam As Integer) As Integer
    End Function
End Class

' 文本段数据结构
Public Class TextSegment
    Public Property Text As String
    Public Property ForeColor As Color
    Public Property BackColor As Color
    Public Property Style As FontStyle
End Class