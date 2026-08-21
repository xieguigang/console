Imports System
Imports System.IO
Imports System.Text
Imports Microsoft.VisualBasic.ApplicationServices.Terminal

Namespace lsd

    ''' <summary>
    ''' Renders the classic <c>ls</c> style output: multi-column listing by
    ''' default, long format with <c>-l</c>, sorting and recursive listing.
    ''' </summary>
    Public Module LsRenderer

        ''' <summary>
        ''' Render all requested paths. Mimics bash ls: a header is printed
        ''' when more than one path is requested or when recursing.
        ''' </summary>
        Public Sub Render(opts As LsdOptions)
            Dim printHeader = opts.Paths.Count > 1

            For Each path As String In opts.Paths
                If opts.Recursive Then
                    RenderRecursive(path, opts, printHeader, 0)
                Else
                    If printHeader Then
                        Console.WriteLine($"{path}:")
                    End If

                    Dim entries = FileSystemScanner.ListPath(path, opts)
                    SortEntries(entries, opts)
                    RenderFlat(entries, opts)
                    If printHeader Then
                        Console.WriteLine()
                    End If
                End If
            Next
        End Sub

        Private Sub RenderRecursive(path As String, opts As LsdOptions, printHeader As Boolean, depth As Integer)
            If printHeader AndAlso depth = 0 Then
                Console.WriteLine($"{path}:")
            ElseIf depth > 0 Then
                Console.WriteLine()
                Console.WriteLine($"{path}:")
            End If

            Dim entries = FileSystemScanner.ListDirectory(path, opts)
            SortEntries(entries, opts)
            RenderFlat(entries, opts)

            For Each entry As FileEntry In entries
                If entry.IsDirectory AndAlso entry.Name <> "." AndAlso entry.Name <> ".." Then
                    RenderRecursive(entry.FullPath, opts, False, depth + 1)
                End If
            Next
        End Sub

        Private Sub RenderFlat(entries As List(Of FileEntry), opts As LsdOptions)
            If entries.Count = 0 Then
                Return
            End If

            If opts.LongFormat Then
                RenderLongFormat(entries, opts)
            Else
                RenderColumns(entries, opts)
            End If
        End Sub

        ' ---- sorting ----
        Public Sub SortEntries(entries As List(Of FileEntry), opts As LsdOptions)
            entries.Sort(Function(a, b)
                             Dim cmp As Integer

                             If opts.SortBySize Then
                                 cmp = b.Size.CompareTo(a.Size)
                                 If cmp <> 0 Then Return ApplyReverse(cmp, opts)
                             ElseIf opts.SortByTime Then
                                 cmp = b.LastWrite.CompareTo(a.LastWrite)
                                 If cmp <> 0 Then Return ApplyReverse(cmp, opts)
                             End If

                             ' directories first (bash ls has no such rule, but keep classic ordering)
                             ' follow bash: plain alphabetical, case-insensitive
                             cmp = String.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase)
                             Return ApplyReverse(cmp, opts)
                         End Function)
        End Sub

        Private Function ApplyReverse(cmp As Integer, opts As LsdOptions) As Integer
            Return If(opts.Reverse, -cmp, cmp)
        End Function

        ' ---- multi column ----
        Private Sub RenderColumns(entries As List(Of FileEntry), opts As LsdOptions)
            Dim displayWidths(entries.Count - 1) As Integer
            Dim maxWidth = 0

            For i As Integer = 0 To entries.Count - 1
                Dim sb As New StringBuilder()
                ConsoleColorTheme.AppendStyledName(sb, entries(i), opts.ColorEnabled)
                ' visual width ignores ansi escapes
                Dim w = UnicodeWidth.GetWidth(StripAnsi(sb.ToString()))
                displayWidths(i) = w + 2
                If displayWidths(i) > maxWidth Then maxWidth = displayWidths(i)
            Next

            Dim termWidth = GetTerminalWidth()
            Dim cols = Math.Max(1, termWidth \ maxWidth)
            If cols > entries.Count Then cols = entries.Count

            Dim rows = CInt(Math.Ceiling(entries.Count / CDbl(cols)))

            For r As Integer = 0 To rows - 1
                Dim line As New StringBuilder()

                For c As Integer = 0 To cols - 1
                    Dim idx = r + c * rows
                    If idx >= entries.Count Then Exit For

                    Dim sb As New StringBuilder()
                    ConsoleColorTheme.AppendStyledName(sb, entries(idx), opts.ColorEnabled)
                    line.Append(sb.ToString())

                    If c < cols - 1 Then
                        line.Append(Space(displayWidths(idx) - UnicodeWidth.GetWidth(StripAnsi(sb.ToString()))))
                    End If
                Next

                Console.WriteLine(line.ToString())
            Next
        End Sub

        ' ---- long format ----
        Private Sub RenderLongFormat(entries As List(Of FileEntry), opts As LsdOptions)
            ' compute max widths for alignment
            Dim maxLinks = 1
            Dim maxOwner = 0
            Dim maxGroup = 0
            Dim maxSize = 0
            Dim maxName = 0

            For Each e As FileEntry In entries
                maxLinks = Math.Max(maxLinks, 1)
                maxOwner = Math.Max(maxOwner, e.Owner.Length)
                maxGroup = Math.Max(maxGroup, e.Group.Length)
                maxSize = Math.Max(maxSize, e.Size.ToString("N0").Length)
                maxName = Math.Max(maxName, UnicodeWidth.GetWidth(e.Name) + 2)
            Next

            For Each e As FileEntry In entries
                Dim line As New StringBuilder()

                ' permission (colored)
                line.Append(ConsoleColorTheme.GetColoredPermission(e.PermissionString, opts.ColorEnabled))
                line.Append(" "c)

                ' hard link count (always 1 on windows)
                line.Append("1".PadLeft(maxLinks))
                line.Append(" "c)

                ' owner / group
                line.Append(e.Owner.PadRight(maxOwner))
                line.Append(" "c)
                line.Append(e.Group.PadRight(maxGroup))
                line.Append(" "c)

                ' size
                line.Append(e.Size.ToString("N0").PadLeft(maxSize))
                line.Append(" "c)

                ' mtime: MMM dd HH:mm or MMM dd  yyyy for old files
                line.Append(FormatTime(e.LastWrite))
                line.Append(" "c)

                ' name with icon + color
                Dim nameSb As New StringBuilder()
                ConsoleColorTheme.AppendStyledName(nameSb, e, opts.ColorEnabled)
                line.Append(nameSb.ToString())

                Console.WriteLine(line.ToString())
            Next
        End Sub

        Private Function FormatTime(dt As DateTime) As String
            Dim now = DateTime.Now
            If dt.Year = now.Year Then
                Return dt.ToString("MMM dd HH:mm")
            Else
                Return dt.ToString("MMM dd  yyyy")
            End If
        End Function

        ' ---- helpers ----
        Private Function GetTerminalWidth() As Integer
            Try
                If Console.BufferWidth > 0 Then
                    Return Console.BufferWidth
                End If
            Catch
            End Try
            Return 80
        End Function

        Private Function Space(n As Integer) As String
            If n <= 0 Then Return ""
            Return New String(" "c, n)
        End Function

        ''' <summary>
        ''' Remove ANSI escape sequences so that visual width can be measured.
        ''' </summary>
        Public Function StripAnsi(text As String) As String
            If String.IsNullOrEmpty(text) Then Return text
            Dim sb As New StringBuilder()
            Dim i = 0

            While i < text.Length
                If text(i) = ChrW(&H1B) Then
                    ' skip until 'm' or any final letter
                    i += 1
                    While i < text.Length AndAlso Not Char.IsLetter(text(i))
                        i += 1
                    End While
                    If i < text.Length Then i += 1
                    Continue While
                End If
                sb.Append(text(i))
                i += 1
            End While

            Return sb.ToString()
        End Function
    End Module
End Namespace
