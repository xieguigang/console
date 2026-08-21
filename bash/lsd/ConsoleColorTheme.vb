Imports System
Imports System.Text
Imports Microsoft.VisualBasic.ApplicationServices.Terminal

Namespace lsd

    ''' <summary>
    ''' Centralised colour theme mapping file types and permission bits to
    ''' <see cref="ConsoleFormat"/> instances. It reuses the ANSI escape
    ''' sequence helpers from <c>Microsoft.VisualBasic.Core</c> so that no
    ''' escape codes are constructed by hand.
    ''' </summary>
    Public Module ConsoleColorTheme

        ' ---- file type colours (classic lsd style) ----
        Private ReadOnly dirColor As New ConsoleFormat(AnsiColor.BrightBlue, Nothing, bold:=True)
        Private ReadOnly symlinkColor As New ConsoleFormat(AnsiColor.BrightCyan, Nothing, bold:=True)
        Private ReadOnly execColor As New ConsoleFormat(AnsiColor.BrightGreen, Nothing, bold:=True)
        Private ReadOnly imageColor As New ConsoleFormat(AnsiColor.Magenta, Nothing)
        Private ReadOnly videoColor As New ConsoleFormat(AnsiColor.BrightMagenta, Nothing)
        Private ReadOnly audioColor As New ConsoleFormat(AnsiColor.Cyan, Nothing)
        Private ReadOnly archiveColor As New ConsoleFormat(AnsiColor.Red, Nothing)
        Private ReadOnly docColor As New ConsoleFormat(AnsiColor.BrightWhite, Nothing)
        Private ReadOnly codeColor As New ConsoleFormat(AnsiColor.BrightYellow, Nothing)
        Private ReadOnly configColor As New ConsoleFormat(AnsiColor.Yellow, Nothing)
        Private ReadOnly hiddenColor As New ConsoleFormat(AnsiColor.BrightBlack, Nothing)
        Private ReadOnly fileColor As New ConsoleFormat(AnsiColor.White, Nothing)

        ' ---- permission bit colours ----
        Private ReadOnly permReadColor As New ConsoleFormat(AnsiColor.BrightGreen, Nothing)
        Private ReadOnly permWriteColor As New ConsoleFormat(AnsiColor.BrightRed, Nothing)
        Private ReadOnly permExecColor As New ConsoleFormat(AnsiColor.BrightYellow, Nothing)
        Private ReadOnly permNoneColor As New ConsoleFormat(AnsiColor.BrightBlack, Nothing)
        Private ReadOnly permTypeColor As New ConsoleFormat(AnsiColor.BrightCyan, Nothing, bold:=True)

        ''' <summary>
        ''' Colour for the full entry name (icon + name), based on type.
        ''' </summary>
        Public Function GetEntryFormat(entry As FileEntry) As ConsoleFormat
            If entry.IsDirectory Then Return dirColor
            If entry.IsSymlink Then Return symlinkColor
            If entry.IsExecutable Then Return execColor

            Dim ext = GetExtension(entry.Name).ToLowerInvariant()

            If IsIn(imageSet, ext) Then Return imageColor
            If IsIn(videoSet, ext) Then Return videoColor
            If IsIn(audioSet, ext) Then Return audioColor
            If IsIn(archiveSet, ext) Then Return archiveColor
            If IsIn(docSet, ext) Then Return docColor
            If IsIn(codeSet, ext) Then Return codeColor
            If IsIn(configSet, ext) Then Return configColor
            If entry.IsHidden Then Return hiddenColor

            Return fileColor
        End Function

        ''' <summary>
        ''' Build the coloured permission string, e.g.
        ''' <c>drwxr-xr-x</c> with per-bit colours.
        ''' </summary>
        Public Function GetColoredPermission(perm As String, Optional colorEnabled As Boolean = True) As String
            If Not colorEnabled Then
                Return perm
            End If

            Dim sb As New StringBuilder()

            For i As Integer = 0 To perm.Length - 1
                Dim ch = perm(i)
                Dim fmt As ConsoleFormat

                If i = 0 Then
                    fmt = permTypeColor
                Else
                    Select Case ch
                        Case "r"c
                            fmt = permReadColor
                        Case "w"c
                            fmt = permWriteColor
                        Case "x"c
                            fmt = permExecColor
                        Case Else
                            fmt = permNoneColor
                    End Select
                End If

                AnsiEscapeCodes.AppendAnsiEscapeSequence(sb, fmt)
                sb.Append(ch)
                sb.Append(AnsiEscapeCodes.Reset)
            Next

            Return sb.ToString()
        End Function

        ''' <summary>
        ''' Append the icon + name with the appropriate colour to the builder.
        ''' </summary>
        Public Sub AppendStyledName(sb As StringBuilder, entry As FileEntry, colorEnabled As Boolean)
            If Not colorEnabled Then
                sb.Append(IconProvider.GetFileIcon(entry))
                sb.Append(" "c)
                sb.Append(entry.Name)
                Return
            End If

            Dim icon = If(entry.IsDirectory, IconProvider.GetDirectoryIcon(), IconProvider.GetFileIcon(entry))
            Dim fmt = GetEntryFormat(entry)

            AnsiEscapeCodes.AppendAnsiEscapeSequence(sb, fmt)
            sb.Append(icon)
            sb.Append(" "c)
            sb.Append(entry.Name)

            If entry.IsSymlink AndAlso Not String.IsNullOrEmpty(entry.LinkTarget) Then
                sb.Append(" -> ")
                sb.Append(entry.LinkTarget)
            End If

            sb.Append(AnsiEscapeCodes.Reset)
        End Sub

        ' extension sets mirrored from IconProvider to keep colour/icon consistent
        Private ReadOnly imageSet As New HashSet(Of String) From {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".webp", ".ico", ".tiff", ".heic"
        }
        Private ReadOnly videoSet As New HashSet(Of String) From {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mpg", ".mpeg"
        }
        Private ReadOnly audioSet As New HashSet(Of String) From {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma"
        }
        Private ReadOnly archiveSet As New HashSet(Of String) From {
            ".zip", ".tar", ".gz", ".bz2", ".xz", ".7z", ".rar", ".tgz", ".zst"
        }
        Private ReadOnly docSet As New HashSet(Of String) From {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".rtf", ".odt", ".txt", ".md"
        }
        Private ReadOnly codeSet As New HashSet(Of String) From {
            ".vb", ".cs", ".cpp", ".c", ".h", ".hpp", ".js", ".ts", ".java", ".py", ".rb",
            ".go", ".rs", ".php", ".html", ".css", ".json", ".xml", ".sql", ".swift", ".kt"
        }
        Private ReadOnly configSet As New HashSet(Of String) From {
            ".ini", ".cfg", ".conf", ".toml", ".yaml", ".yml", ".env", ".properties", ".gitignore"
        }

        Private Function IsIn(set_ As HashSet(Of String), ext As String) As Boolean
            Return set_.Contains(ext)
        End Function

        Private Function GetExtension(name As String) As String
            Dim dot = name.LastIndexOf("."c)
            If dot < 0 Then Return ""
            Return name.Substring(dot)
        End Function
    End Module
End Namespace
