' ---------------------------------------------------------------------------
' TerminalBuffer
'
' A character-grid model of a terminal screen. Unlike RichTextBox (which only
' understands "append text at the end"), a real terminal renders to a fixed
' grid of rows x cols. Applications such as htop / btop clear the screen
' (ESC[2J / ESC[3J) and then re-draw every frame from the top-left (ESC[H).
' This buffer implements that grid semantics so such programs can be rendered
' correctly:
'
'   * ESC[H / ESC[f      -> CursorPosition  (move the cursor)
'   * ESC[A/B/C/D        -> cursor relative moves
'   * ESC[J (0/1/2/3)    -> erase in display (incl. full clear)
'   * ESC[K (0/1/2)      -> erase in line
'   * ESC[m             -> SGR colours (reuses AnsiEscapeRenderer palette)
'   * CR / LF / BS / TAB -> cursor + scroll behaviour
'
' The owning control (TerminalControl) feeds output into ProcessAnsi / PutText
' and then paints the whole buffer to the RichTextBox once per frame.
' ---------------------------------------------------------------------------

Imports System.Drawing
Imports System.Text

Namespace Console

    ''' <summary>
    ''' A single cell on the terminal grid.
    ''' </summary>
    Public Structure CharCell
        Public Ch As Char
        Public ForeColor As Color
        Public BackColor As Color
        Public Style As FontStyle

        Public Shared ReadOnly Empty As New CharCell() With {
            .Ch = " "c,
            .ForeColor = Color.White,
            .BackColor = Color.Black,
            .Style = FontStyle.Regular
        }
    End Structure

    Public Class TerminalBuffer

        '  Control characters (System.Windows.Forms.ControlChars lacks Escape/Bel,
        '  so define them explicitly to stay framework-version agnostic).
        Private Const Esc As Char = ChrW(27)
        Private Const Bel As Char = ChrW(7)

        ' ---- dimensions -----------------------------------------------------
        Private _rows As Integer
        Private _cols As Integer

        ' ---- grid -----------------------------------------------------------
        ' _cells(row, col)
        Private _cells As CharCell(,)

        ' ---- cursor ---------------------------------------------------------
        Private _cursorRow As Integer = 0
        Private _cursorCol As Integer = 0

        ' ---- saved cursor (ESC 7 / ESC 8) ----------------------------------
        Private _savedRow As Integer = 0
        Private _savedCol As Integer = 0

        ' ---- current SGR state (colour / style) ----------------------------
        Public State As New AnsiEscapeRenderer.AnsiTerminalState()

        ' ---- pending tab stops are every 8 columns -------------------------
        Private Const TabStop As Integer = 8

        Public Sub New(rows As Integer, cols As Integer)
            _rows = System.Math.Max(1, rows)
            _cols = System.Math.Max(1, cols)
            Reallocate(_rows, _cols)
        End Sub

        Public Property Rows As Integer
            Get
                Return _rows
            End Get
            Set(value As Integer)
                value = System.Math.Max(1, value)
                If value <> _rows Then
                    Reallocate(value, _cols)
                End If
            End Set
        End Property

        Public Property Cols As Integer
            Get
                Return _cols
            End Get
            Set(value As Integer)
                value = System.Math.Max(1, value)
                If value <> _cols Then
                    Reallocate(_rows, value)
                End If
            End Set
        End Property

        Public ReadOnly Property CursorRow As Integer
            Get
                Return _cursorRow
            End Get
        End Property

        Public ReadOnly Property CursorCol As Integer
            Get
                Return _cursorCol
            End Get
        End Property

        ''' <summary>
        ''' Total number of character cells (rows * cols). Used by the renderer
        ''' to reserve capacity.
        ''' </summary>
        Public ReadOnly Property CellCount As Integer
            Get
                Return _rows * _cols
            End Get
        End Property

        Private Sub Reallocate(rows As Integer, cols As Integer)
            Dim old = _cells
            Dim oldRows = If(old Is Nothing, 0, old.GetLength(0))
            Dim oldCols = If(old Is Nothing, 0, old.GetLength(1))

            _cells = New CharCell(rows - 1, cols - 1) {}
            For r As Integer = 0 To rows - 1
                For c As Integer = 0 To cols - 1
                    _cells(r, c) = CharCell.Empty
                Next
            Next

            '  Copy whatever overlaps (keeps the bottom of the previous screen
            '  when growing; behaviour on shrink is best-effort).
            If old IsNot Nothing Then
                Dim copyRows = System.Math.Min(rows, oldRows)
                Dim copyCols = System.Math.Min(cols, oldCols)
                For r As Integer = 0 To copyRows - 1
                    For c As Integer = 0 To copyCols - 1
                        _cells(r, c) = old(r, c)
                    Next
                Next
            End If

            _rows = rows
            _cols = cols
            _cursorRow = System.Math.Min(_cursorRow, rows - 1)
            _cursorCol = System.Math.Min(_cursorCol, cols - 1)
        End Sub

        ' ====================================================================
        '  Plain text (no escape sequences)
        ' ====================================================================

        ''' <summary>
        ''' Writes a run of plain text honouring CR / LF / BS / TAB semantics.
        ''' </summary>
        Public Sub PutText(text As String)
            If String.IsNullOrEmpty(text) Then Return
            For Each ch As Char In text
                Select Case ch
                    Case ControlChars.Cr
                        CarriageReturn()
                    Case ControlChars.Lf, vbLf
                        LineFeed()
                    Case ControlChars.Back
                        Backspace()
                    Case ControlChars.Tab
                        PutChar(" "c)
                        While _cursorCol Mod TabStop <> 0
                            PutChar(" "c)
                        End While
                    Case Else
                        PutChar(ch)
                End Select
            Next
        End Sub

        ''' <summary>
        ''' Places a single character at the cursor and advances the cursor,
        ''' wrapping / scrolling at the right edge.
        ''' </summary>
        Public Sub PutChar(ch As Char)
            Dim cell As CharCell = CharCell.Empty
            cell.Ch = ch
            cell.ForeColor = State.ForeColor
            cell.BackColor = State.BackColor
            cell.Style = State.Style
            _cells(_cursorRow, _cursorCol) = cell

            _cursorCol += 1
            If _cursorCol >= _cols Then
                _cursorCol = 0
                LineFeed()
            End If
        End Sub

        Public Sub CarriageReturn()
            _cursorCol = 0
        End Sub

        Public Sub LineFeed()
            _cursorRow += 1
            If _cursorRow >= _rows Then
                ScrollUp()
                _cursorRow = _rows - 1
            End If
        End Sub

        Public Sub Backspace()
            If _cursorCol > 0 Then
                _cursorCol -= 1
            ElseIf _cursorRow > 0 Then
                _cursorRow -= 1
                _cursorCol = _cols - 1
            End If
            '  Erase the cell the cursor moved back over (terminal semantics).
            _cells(_cursorRow, _cursorCol) = CharCell.Empty
        End Sub

        ''' <summary>
        ''' Scrolls the whole screen up by one row, discarding the top row and
        ''' clearing the (new) bottom row.
        ''' </summary>
        Public Sub ScrollUp()
            For r As Integer = 1 To _rows - 1
                For c As Integer = 0 To _cols - 1
                    _cells(r - 1, c) = _cells(r, c)
                Next
            Next
            For c As Integer = 0 To _cols - 1
                _cells(_rows - 1, c) = CharCell.Empty
            Next
        End Sub

        ' ====================================================================
        '  Cursor movement (ESC[A/B/C/D, ESC[H/ESC[f)
        ' ====================================================================

        Public Sub CursorUp(n As Integer)
            _cursorRow = System.Math.Max(0, _cursorRow - System.Math.Max(1, n))
        End Sub

        Public Sub CursorDown(n As Integer)
            _cursorRow = System.Math.Min(_rows - 1, _cursorRow + System.Math.Max(1, n))
        End Sub

        Public Sub CursorForward(n As Integer)
            _cursorCol = System.Math.Min(_cols - 1, _cursorCol + System.Math.Max(1, n))
        End Sub

        Public Sub CursorBack(n As Integer)
            _cursorCol = System.Math.Max(0, _cursorCol - System.Math.Max(1, n))
        End Sub

        ''' <summary>
        ''' Cursor position (1-based in the CSI, stored 0-based internally).
        ''' </summary>
        Public Sub CursorPosition(row As Integer, col As Integer)
            If row < 1 Then row = 1
            If col < 1 Then col = 1
            _cursorRow = System.Math.Min(_rows - 1, row - 1)
            _cursorCol = System.Math.Min(_cols - 1, col - 1)
        End Sub

        Public Sub SaveCursor()
            _savedRow = _cursorRow
            _savedCol = _cursorCol
        End Sub

        Public Sub RestoreCursor()
            _cursorRow = _savedRow
            _cursorCol = _savedCol
        End Sub

        ' ====================================================================
        '  Erase operations (ESC[J, ESC[K)
        ' ====================================================================

        ''' <summary>
        ''' Erase in display.
        '''   0 -> cursor to end of screen
        '''   1 -> start of screen to cursor
        '''   2 -> entire screen (cursor unchanged)
        '''   3 -> entire screen AND scrollback (same as 2 here, grid only)
        ''' </summary>
        Public Sub EraseInDisplay(mode As Integer)
            Select Case mode
                Case 1
                    For r As Integer = 0 To _cursorRow
                        For c As Integer = 0 To _cols - 1
                            If r = _cursorRow AndAlso c >= _cursorCol Then Exit For
                            _cells(r, c) = CharCell.Empty
                        Next
                    Next
                Case 2, 3
                    For r As Integer = 0 To _rows - 1
                        For c As Integer = 0 To _cols - 1
                            _cells(r, c) = CharCell.Empty
                        Next
                    Next
                Case Else ' 0
                    For r As Integer = _cursorRow To _rows - 1
                        For c As Integer = 0 To _cols - 1
                            If r = _cursorRow AndAlso c < _cursorCol Then Continue For
                            _cells(r, c) = CharCell.Empty
                        Next
                    Next
            End Select
        End Sub

        ''' <summary>
        ''' Erase in line.
        '''   0 -> cursor to end of line
        '''   1 -> start of line to cursor
        '''   2 -> entire line
        ''' </summary>
        Public Sub EraseInLine(mode As Integer)
            Select Case mode
                Case 1
                    For c As Integer = 0 To _cursorCol
                        _cells(_cursorRow, c) = CharCell.Empty
                    Next
                Case 2
                    For c As Integer = 0 To _cols - 1
                        _cells(_cursorRow, c) = CharCell.Empty
                    Next
                Case Else ' 0
                    For c As Integer = _cursorCol To _cols - 1
                        _cells(_cursorRow, c) = CharCell.Empty
                    Next
            End Select
        End Sub

        ' ====================================================================
        '  ANSI / xterm parsing
        ' ====================================================================

        Private Enum ParserState
            Normal
            Escape
            Csi
        End Enum

        ''' <summary>
        ' Processes a chunk of text that may contain ANSI escape sequences and
        ' applies them to the grid. Plain text is written via PutText.
        ' </summary>
        Public Sub ProcessAnsi(text As String)
            If String.IsNullOrEmpty(text) Then Return

            Dim st As ParserState = ParserState.Normal
            Dim csi As New StringBuilder()
            Dim i As Integer = 0

            While i < text.Length
                Dim ch As Char = text(i)

                Select Case st
                    Case ParserState.Normal
                        If ch = Esc Then
                            st = ParserState.Escape
                        Else
                            PutText(ch)
                        End If

                    Case ParserState.Escape
                        If ch = "["c Then
                            csi.Clear()
                            st = ParserState.Csi
                        ElseIf ch = "]"c Then
                            '  OSC (e.g. set title). Consume up to BEL/ST.
                            i = ConsumeOsc(text, i)
                            st = ParserState.Normal
                        ElseIf ch = "7"c Then
                            SaveCursor()
                            st = ParserState.Normal
                        ElseIf ch = "8"c Then
                            RestoreCursor()
                            st = ParserState.Normal
                        Else
                            '  Unrecognised two-byte escape: ignore.
                            st = ParserState.Normal
                        End If

                    Case ParserState.Csi
                        If (ch >= "0"c AndAlso ch <= "9"c) OrElse ch = ";"c OrElse ch = ":"c Then
                            csi.Append(ch)
                        Else
                            HandleCsi(ch, csi.ToString())
                            csi.Clear()
                            st = ParserState.Normal
                        End If
                End Select

                i += 1
            End While
        End Sub

        Private Shared Function ConsumeOsc(text As String, start As Integer) As Integer
            '  start points at the ']' character. Scan for BEL (07) or ST (ESC \).
            Dim i As Integer = start + 1
            While i < text.Length
                If text(i) = Bel Then
                    Return i
                End If
                If text(i) = Esc AndAlso i + 1 < text.Length AndAlso text(i + 1) = "\"c Then
                    Return i + 1
                End If
                i += 1
            End While
            Return i
        End Function

        Private Sub HandleCsi(finalChar As Char, paramStr As String)
            Dim p() As String = If(String.IsNullOrEmpty(paramStr), New String() {""}, paramStr.Split(";"c))
            Dim functionChar As Char = finalChar

            Select Case functionChar
                Case "H"c, "f"c
                    Dim row As Integer = ParseParam(p, 0, 1)
                    Dim col As Integer = ParseParam(p, 1, 1)
                    CursorPosition(row, col)

                Case "A"c
                    CursorUp(ParseParam(p, 0, 1))
                Case "B"c
                    CursorDown(ParseParam(p, 0, 1))
                Case "C"c
                    CursorForward(ParseParam(p, 0, 1))
                Case "D"c
                    CursorBack(ParseParam(p, 0, 1))

                Case "E"c
                    '  Cursor next line.
                    CursorDown(ParseParam(p, 0, 1))
                    _cursorCol = 0
                Case "F"c
                    '  Cursor previous line.
                    CursorUp(ParseParam(p, 0, 1))
                    _cursorCol = 0
                Case "G"c
                    _cursorCol = System.Math.Min(_cols - 1, ParseParam(p, 0, 1) - 1)

                Case "J"c
                    EraseInDisplay(ParseParam(p, 0, 0))
                Case "K"c
                    EraseInLine(ParseParam(p, 0, 0))

                Case "m"c
                    '  SGR - reuse the shared palette / parser.
                    AnsiEscapeRenderer.ApplySgr(State, paramStr)

                Case "s"c
                    SaveCursor()
                Case "u"c
                    RestoreCursor()

                Case "r"c
                    '  Set scrolling region - not needed for the grid model,
                    '  ignore so output keeps flowing.
                Case Else
                    '  Unhandled CSI - ignore.
            End Select
        End Sub

        Private Shared Function ParseParam(p() As String, index As Integer, defaultValue As Integer) As Integer
            If index >= p.Length Then Return defaultValue
            Dim v As Integer = 0
            If Integer.TryParse(p(index), v) AndAlso v > 0 Then Return v
            Return defaultValue
        End Function

        ' ====================================================================
        '  Rendering support
        ' ====================================================================

        ''' <summary>
        ''' Returns the cell at the given grid coordinates.
        ''' </summary>
        Public Function GetCell(row As Integer, col As Integer) As CharCell
            Return _cells(row, col)
        End Function

        ''' <summary>
        ''' Clears the entire grid and resets the cursor to the home position.
        ''' </summary>
        Public Sub Reset()
            For r As Integer = 0 To _rows - 1
                For c As Integer = 0 To _cols - 1
                    _cells(r, c) = CharCell.Empty
                Next
            Next
            _cursorRow = 0
            _cursorCol = 0
            State.Reset()
        End Sub

    End Class

End Namespace
