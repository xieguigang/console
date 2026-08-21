Imports System
Imports System.Collections.Generic

Namespace lsd

    ''' <summary>
    ''' Maps file types and extensions to Unicode / nerd-style icons,
    ''' following the classic <c>lsd</c> (Rust) visual conventions.
    ''' </summary>
    Public Module IconProvider

        ' directory icons
        Private Const DirIcon As String = "📁" ' 📁
        Private Const DirOpenIcon As String = "📂" ' 📂

        ' generic file
        Private Const FileIcon As String = "📄" ' 📄

        ' special types
        Private Const LinkIcon As String = "🔗" ' 🔗
        Private Const ExecIcon As String = "⚙️" ' ⚙
        Private Const ImageIcon As String = "🖼️" ' 🖼
        Private Const VideoIcon As String = "🎞️" ' 🎞
        Private Const AudioIcon As String = "🎵" ' 🎵
        Private Const ArchiveIcon As String = "🗜️" ' 🗜
        Private Const DocIcon As String = "📝" ' 📝
        Private Const CodeIcon As String = "📜" ' 📜
        Private Const VBCodeIcon As String = "📑"
        Private Const ConfigIcon As String = "🔧" ' 🔧
        Private Const LockIcon As String = "🔒" ' 🔒

        Private Const BiologyIcon As String = "🧬"

        Private Const DataIcon As String = "💾"

        Private ReadOnly bioExt As New HashSet(Of String) From {
            ".fna", ".fa", ".gb", ".gbk", ".gff"
        }

        Private ReadOnly dataExt As New HashSet(Of String) From {
            ".dat"
        }

        Private ReadOnly VBExt As New HashSet(Of String) From {
            ".vb", ".vbproj", ".vbs", ".bas"
        }

        Private ReadOnly imageExt As New HashSet(Of String) From {
            ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".svg", ".webp", ".ico", ".tiff", ".heic"
        }

        Private ReadOnly videoExt As New HashSet(Of String) From {
            ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".flv", ".webm", ".mpg", ".mpeg"
        }

        Private ReadOnly audioExt As New HashSet(Of String) From {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma"
        }

        Private ReadOnly archiveExt As New HashSet(Of String) From {
            ".zip", ".tar", ".gz", ".bz2", ".xz", ".7z", ".rar", ".tgz", ".zst"
        }

        Private ReadOnly docExt As New HashSet(Of String) From {
            ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".rtf", ".odt", ".txt", ".md"
        }

        Private ReadOnly codeExt As New HashSet(Of String) From {
            ".cs", ".cpp", ".c", ".h", ".hpp", ".js", ".ts", ".java", ".py", ".rb",
            ".go", ".rs", ".php", ".html", ".css", ".json", ".xml", ".sql", ".swift", ".kt"
        }

        Private ReadOnly configExt As New HashSet(Of String) From {
            ".ini", ".cfg", ".conf", ".toml", ".yaml", ".yml", ".env", ".properties", ".gitignore"
        }

        ''' <summary>
        ''' Get the icon for a directory entry (open if it has children shown).
        ''' </summary>
        Public Function GetDirectoryIcon() As String
            Return DirIcon
        End Function

        ''' <summary>
        ''' Get the icon for a file entry based on its type and extension.
        ''' </summary>
        Public Function GetFileIcon(entry As FileEntry) As String
            If entry.IsSymlink Then
                Return LinkIcon
            End If

            If entry.IsExecutable Then
                Return ExecIcon
            End If

            Dim ext = GetExtension(entry.Name).ToLowerInvariant()

            If dataExt.Contains(ext) Then Return DataIcon
            If VBExt.Contains(ext) Then Return VBCodeIcon
            If bioExt.Contains(ext) Then Return BiologyIcon

            If imageExt.Contains(ext) Then Return ImageIcon
            If videoExt.Contains(ext) Then Return VideoIcon
            If audioExt.Contains(ext) Then Return AudioIcon
            If archiveExt.Contains(ext) Then Return ArchiveIcon
            If docExt.Contains(ext) Then Return DocIcon
            If codeExt.Contains(ext) Then Return CodeIcon
            If configExt.Contains(ext) Then Return ConfigIcon

            If entry.IsHidden Then Return LockIcon

            Return FileIcon
        End Function

        Private Function GetExtension(name As String) As String
            Dim dot = name.LastIndexOf("."c)
            If dot < 0 Then Return ""
            Return name.Substring(dot)
        End Function
    End Module
End Namespace
