' ---------------------------------------------------------------------------
' TerminalControl
'
' A drop-in replacement for ConsoleControl that renders to a fixed character
' grid (TerminalBuffer) instead of appending text directly to the RichTextBox.
'
' Why a grid is needed:
'   Programs such as htop / btop clear the screen (ESC[2J / ESC[3J) and then
'   re-draw every frame from the top-left (ESC[H). RichTextBox only understands
'   "append at the end", so such frames were appended below the previous one
'   and the picture became garbled. TerminalBuffer implements real cursor /
'   erase / clear-screen semantics, so each frame is drawn in place.
'
' It derives from ConsoleControl, so every property, event and the keyboard /
' input handling (including the Ctrl+C -> raw ETX fix) is reused unchanged.
' Only the rendering path is overridden:
'   * WriteAnsiEscape -> parse into the grid, then paint the grid
'   * WriteOutput     -> feed plain/ANSI text into the grid, then paint
'   * WriteInput      -> forward to the backend (optionally echoing locally)
'   * ClearOutput     -> clear the grid
'   * StartProcess    -> reset the grid
' ---------------------------------------------------------------------------

Imports System.Drawing
Imports System.Text
Imports System.Windows.Forms

Namespace Console

    Public Class TerminalControl
        Inherits ConsoleControl

        '  The character-grid terminal screen model.
        Private _buffer As TerminalBuffer

        '  Re-used to accumulate a (possibly split) ANSI escape sequence across
        '  output batches, exactly like the base control's ansiBuffer.
        Private _ansiBuffer As New StringBuilder()

        '  Cached grid size; recomputed on resize.
        Private _cols As Integer = 80
        Private _rows As Integer = 24

        '  A font used to render the grid; mirrors the RichTextBox font.
        Private _gridFont As Font

        '  ESC control character (System.Windows.Forms.ControlChars lacks it).
        Private Const Esc As Char = ChrW(27)

        Public Sub New()
            ' 20260802 parent sub new may call codes to initialize the grid font and buffer object
            MyBase.New()

            ' try to avoid of re-create the font and buffer object when the parent sub new is called
            ' to avoid lost the initlaized status
            If _gridFont Is Nothing Then _gridFont = New Font("Consolas", 9, FontStyle.Regular)
            If _buffer Is Nothing Then _buffer = New TerminalBuffer(_rows, _cols)
        End Sub

        ' ====================================================================
        '  Grid sizing
        ' ====================================================================

        ''' <summary>
        ''' Recomputes how many character columns/rows fit in the RichTextBox,
        ''' and resizes the grid accordingly.
        ''' </summary>
        Private Sub RecomputeGridSize()
            Dim rtb = richTextBoxConsole
            If rtb Is Nothing OrElse rtb.IsDisposed Then Return

            If rtb.Font IsNot Nothing Then
                _gridFont = New Font(rtb.Font.FontFamily, rtb.Font.Size, FontStyle.Regular)
            End If
            If _buffer Is Nothing Then
                _buffer = New TerminalBuffer(_rows, _cols)
            End If

            '  Measure a single monospace cell.
            Dim probe As String = New String("M"c, 10)
            Dim flags As TextFormatFlags = TextFormatFlags.NoPadding Or TextFormatFlags.SingleLine
            Dim lineSize As Size = TextRenderer.MeasureText(probe, _gridFont, New Size(Integer.MaxValue, Integer.MaxValue), flags)
            Dim charW As Integer = If(lineSize.Width > 0, lineSize.Width \ probe.Length, 8)
            Dim charH As Integer = If(lineSize.Height > 0, lineSize.Height, 16)

            Dim clientW As Integer = System.Math.Max(1, rtb.ClientSize.Width - 4)
            Dim clientH As Integer = System.Math.Max(1, rtb.ClientSize.Height - 4)

            Dim cols As Integer = System.Math.Max(1, clientW \ charW)
            Dim rows As Integer = System.Math.Max(1, clientH \ charH)

            If cols <> _cols OrElse rows <> _rows Then
                _cols = cols
                _rows = rows
                _buffer.Cols = cols
                _buffer.Rows = rows
            End If
        End Sub

        Protected Overrides Sub OnLoad(e As EventArgs)
            MyBase.OnLoad(e)
            RecomputeGridSize()
        End Sub

        Protected Overrides Sub OnResize(e As EventArgs)
            MyBase.OnResize(e)
            RecomputeGridSize()
            Call RenderToRichTextBox()
        End Sub

        ' ====================================================================
        '  Overridden rendering
        ' ====================================================================

        ''' <summary>
        ''' Accepts a (possibly partial) chunk of output. ANSI escape sequences
        ''' are accumulated until complete, then parsed into the grid and the
        ''' whole grid is painted once.
        ''' </summary>
        Public Overrides Sub WriteAnsiEscape(ansiText As String)
            If ansiText Is Nothing Then Return
            _ansiBuffer.Append(ansiText)

            Dim text As String = _ansiBuffer.ToString()
            '  Wait for a complete escape sequence before painting. A split
            '  sequence (ended mid-CSI) would be parsed incorrectly otherwise.
            Dim escIdx As Integer = text.IndexOf(Esc)
            If escIdx >= 0 Then
                Dim endIdx As Integer = text.IndexOf(Esc, escIdx + 1)
                If endIdx < 0 Then
                    '  Only one escape present: ensure it is terminated.
                    If Not text.EndsWith("["c) AndAlso Not IsCompleteCsi(text, escIdx) Then
                        Return
                    End If
                End If
            End If

            _ansiBuffer.Clear()
            _buffer.ProcessAnsi(text)

            Call Me.Invoke(Sub() RenderToRichTextBox())
        End Sub

        ''' <summary>
        ''' True when the escape sequence starting at <paramref name="escIdx"/>
        ''' is a complete CSI (terminated by a final byte in @[A-Z\].
        ''' </summary>
        Private Shared Function IsCompleteCsi(text As String, escIdx As Integer) As Boolean
            If escIdx + 1 >= text.Length OrElse text(escIdx + 1) <> "["c Then Return True
            Dim j As Integer = escIdx + 2
            While j < text.Length
                Dim c As Char = text(j)
                If (c >= "A"c AndAlso c <= "Z"c) OrElse c = "["c Then Return True
                If c = Esc Then Return False
                j += 1
            End While
            Return False
        End Function

        ''' <summary>
        ''' Writes plain / ANSI-mixed output. Mirrors the base class logic but
        ''' routes everything through the character grid so plain text and ANSI
        ''' art share the same (correct) coordinate space.
        ''' </summary>
        Public Overrides Sub WriteOutput(output As String, color As Color)
            If output Is Nothing Then Return

            '  Fast path: nothing ANSI -> straight into the grid.
            If output.IndexOf(Esc) < 0 Then
                _buffer.State.ForeColor = color
                _buffer.PutText(output)
                Call RenderToRichTextBox()
                Return
            End If

            '  Contains escape sequences: forward through the buffered ANSI path.
            Call WriteAnsiEscape(output)
        End Sub

        ''' <summary>
        ''' Forwards input to the backend. When <paramref name="echo"/> is set
        ''' the text is also written into the grid (local-echo style); for an SSH
        ''' shell the remote already echoes, so echo should be false.
        ''' </summary>
        Public Overrides Sub WriteInput(input As String, color As Color, echo As Boolean)
            If echo Then
                _buffer.State.ForeColor = color
                _buffer.PutText(input)
                Call RenderToRichTextBox()
            End If
            If m_console IsNot Nothing Then
                m_console.WriteInput(input)
            End If
        End Sub

        ''' <summary>
        ''' Clears the screen.
        ''' </summary>
        Public Overrides Sub ClearOutput()
            _buffer.Reset()
            _ansiBuffer.Clear()
            Call RenderToRichTextBox()
        End Sub

        ''' <summary>
        ''' Resets the grid when a new session/process starts.
        ''' </summary>
        Public Overrides Sub StartProcess()
            RecomputeGridSize()
            _buffer.Reset()
            _ansiBuffer.Clear()
            MyBase.StartProcess()
        End Sub

        ' ====================================================================
        '  Painting the grid into the RichTextBox
        ' ====================================================================

        Private Sub RenderToRichTextBox()
            Dim rtb = richTextBoxConsole
            If rtb Is Nothing OrElse rtb.IsDisposed OrElse Not rtb.IsHandleCreated Then Return

            If _cols <> _buffer.Cols OrElse _rows <> _buffer.Rows Then
                RecomputeGridSize()
            End If

            rtb.SuspendLayout()
            rtb.Clear()
            rtb.SelectionStart = 0
            rtb.SelectionLength = 0
            rtb.SelectionFont = _gridFont

            Dim rows As Integer = _buffer.Rows
            Dim cols As Integer = _buffer.Cols

            Dim runCh As New StringBuilder()
            Dim runFore As Color = Color.White
            Dim runBack As Color = Color.Black
            Dim runStyle As FontStyle = FontStyle.Regular
            Dim runActive As Boolean = False

            Dim flush = Sub()
                            If Not runActive OrElse runCh.Length = 0 Then
                                runActive = False
                                Return
                            End If
                            rtb.SelectionColor = runFore
                            rtb.SelectionBackColor = runBack
                            rtb.SelectionFont = New Font(_gridFont.FontFamily, _gridFont.Size, runStyle)
                            rtb.AppendText(runCh.ToString())
                            runCh.Clear()
                            runActive = False
                        End Sub

            For r As Integer = 0 To rows - 1
                For c As Integer = 0 To cols - 1
                    Dim cell As CharCell = _buffer.GetCell(r, c)
                    Dim ch As Char = If(cell.Ch = ControlChars.NullChar, " "c, cell.Ch)
                    If ch = ControlChars.NullChar Then ch = " "c

                    If runActive AndAlso
                       (cell.ForeColor <> runFore OrElse cell.BackColor <> runBack OrElse cell.Style <> runStyle) Then
                        flush()
                    End If

                    If Not runActive Then
                        runFore = cell.ForeColor
                        runBack = cell.BackColor
                        runStyle = cell.Style
                        runActive = True
                    End If

                    runCh.Append(ch)
                Next
                flush()
                If r < rows - 1 Then
                    rtb.AppendText(vbCrLf)
                End If
            Next

            '  Place the caret at the terminal cursor so local typing lines up.
            Try
                Dim caretRow As Integer = System.Math.Min(_buffer.CursorRow, rows - 1)
                Dim caretCol As Integer = System.Math.Min(_buffer.CursorCol, cols - 1)
                Dim idx As Integer = 0
                For r As Integer = 0 To caretRow - 1
                    idx += cols + vbCrLf.Length
                Next
                idx += caretCol
                If idx < rtb.TextLength Then
                    rtb.SelectionStart = idx
                    rtb.SelectionLength = 0
                Else
                    rtb.SelectionStart = rtb.TextLength
                    rtb.SelectionLength = 0
                End If
            Catch
                rtb.SelectionStart = rtb.TextLength
                rtb.SelectionLength = 0
            End Try

            rtb.ResumeLayout()
        End Sub

    End Class

End Namespace
