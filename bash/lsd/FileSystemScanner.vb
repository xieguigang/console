Imports System
Imports System.IO
Imports System.Collections.Generic
Imports System.Text

Namespace lsd

    ''' <summary>
    ''' A single file system entry with the information required for both
    ''' the <c>ls</c> and <c>tree</c> rendering.
    ''' </summary>
    Public Class FileEntry

        Public Name As String
        Public FullPath As String
        Public IsDirectory As Boolean
        Public IsHidden As Boolean
        Public IsSystem As Boolean
        Public IsSymlink As Boolean
        Public IsExecutable As Boolean
        Public Size As Long
        Public LastWrite As DateTime
        Public LinkTarget As String
        Public PermissionString As String
        Public Owner As String
        Public Group As String

        ''' <summary>
        ''' Build a human readable permission string in the bash style, e.g.
        ''' <c>drwxr-xr-x</c>. The first character encodes the entry type.
        ''' </summary>
        Public Shared Function BuildPermissionString(info As FileSystemInfo) As String
            Dim sb As New StringBuilder()
            Dim attrs = info.Attributes

            ' type character
            If (attrs And FileAttributes.Directory) = FileAttributes.Directory Then
                sb.Append("d"c)
            ElseIf (attrs And FileAttributes.ReparsePoint) = FileAttributes.ReparsePoint Then
                sb.Append("l"c)
            Else
                sb.Append("-"c)
            End If

            ' owner
            sb.Append(If((attrs And FileAttributes.ReadOnly) = 0, "r"c, "-"c))
            sb.Append(If((attrs And FileAttributes.ReadOnly) = 0 AndAlso Not IsDirectoryReadOnly(info), "w"c, "-"c))
            sb.Append("x"c) ' execute bit approximated (windows has no per-user x)

            ' group
            sb.Append("r"c)
            sb.Append(If((attrs And FileAttributes.ReadOnly) = 0, "w"c, "-"c))
            sb.Append("x"c)

            ' other
            sb.Append("r"c)
            sb.Append(If((attrs And FileAttributes.ReadOnly) = 0, "w"c, "-"c))
            sb.Append("x"c)

            Return sb.ToString()
        End Function

        Private Shared Function IsDirectoryReadOnly(info As FileSystemInfo) As Boolean
            ' directories on windows are never marked ReadOnly by the shell,
            ' so treat them as writable unless the readonly attribute is set.
            Return (info.Attributes And FileAttributes.ReadOnly) = FileAttributes.ReadOnly
        End Function
    End Class

    ''' <summary>
    ''' Enumerates directories and files and produces <see cref="FileEntry"/>
    ''' instances. Handles hidden/almost-all filtering and symlink detection.
    ''' </summary>
    Public Module FileSystemScanner

        ''' <summary>
        ''' Enumerate the direct children of <paramref name="path"/>.
        ''' </summary>
        Public Function ListDirectory(path As String, opts As LsdOptions) As List(Of FileEntry)
            Dim dir As New DirectoryInfo(path)
            Dim entries As New List(Of FileEntry)

            Dim infos As FileSystemInfo()

            Try
                infos = dir.GetFileSystemInfos()
            Catch ex As Exception
                Console.Error.WriteLine($"lsd: cannot access '{path}': {ex.Message}")
                Return entries
            End Try

            For Each info As FileSystemInfo In infos
                If info.Name = "." OrElse info.Name = ".." Then
                    If opts.ShowAll Then
                        entries.Add(CreateEntry(info))
                    End If
                    Continue For
                End If

                Dim isHidden = (info.Attributes And FileAttributes.Hidden) = FileAttributes.Hidden

                If isHidden AndAlso Not opts.ShowAll AndAlso Not opts.AlmostAll Then
                    Continue For
                End If

                entries.Add(CreateEntry(info))
            Next

            Return entries
        End Function

        ''' <summary>
        ''' Enumerate a single path which may be a file or a directory.
        ''' Returns the single entry for a file, or the directory children.
        ''' </summary>
        Public Function ListPath(path As String, opts As LsdOptions) As List(Of FileEntry)
            If Directory.Exists(path) Then
                Return ListDirectory(path, opts)
            End If

            If File.Exists(path) Then
                Dim info As New FileInfo(path)
                Return New List(Of FileEntry) From {CreateEntry(info)}
            End If

            Console.Error.WriteLine($"lsd: cannot access '{path}': No such file or directory")
            Return New List(Of FileEntry)()
        End Function

        Public Function CreateEntry(info As FileSystemInfo) As FileEntry
            Dim entry As New FileEntry()
            entry.Name = info.Name
            entry.FullPath = info.FullName
            entry.IsDirectory = (info.Attributes And FileAttributes.Directory) = FileAttributes.Directory
            entry.IsHidden = (info.Attributes And FileAttributes.Hidden) = FileAttributes.Hidden
            entry.IsSystem = (info.Attributes And FileAttributes.System) = FileAttributes.System
            entry.IsSymlink = (info.Attributes And FileAttributes.ReparsePoint) = FileAttributes.ReparsePoint
            entry.LastWrite = info.LastWriteTime
            entry.PermissionString = FileEntry.BuildPermissionString(info)
            entry.Owner = Environment.UserName
            entry.Group = Environment.UserName

            If entry.IsDirectory Then
                entry.Size = 0
                entry.IsExecutable = False
            Else
                Dim fi As FileInfo = DirectCast(info, FileInfo)
                entry.Size = fi.Length
                entry.IsExecutable = IsExecutableByExtension(fi.Extension)
            End If

            If entry.IsSymlink AndAlso Not entry.IsDirectory Then
                Try
                    entry.LinkTarget = TryGetLinkTarget(info.FullName)
                Catch
                    entry.LinkTarget = ""
                End Try
            ElseIf entry.IsSymlink Then
                Try
                    entry.LinkTarget = TryGetLinkTarget(info.FullName)
                Catch
                    entry.LinkTarget = ""
                End Try
            End If

            Return entry
        End Function

        Private Function TryGetLinkTarget(path As String) As String
            Try
                Dim target = File.ResolveLinkTarget(path, False)
                If target IsNot Nothing Then
                    Return target.FullName
                End If
            Catch
            End Try
            Return ""
        End Function

        Private Function IsExecutableByExtension(ext As String) As Boolean
            Select Case ext.ToLowerInvariant()
                Case ".exe", ".bat", ".cmd", ".com", ".ps1", ".sh", ".py", ".jar", ".vbs", ".msi"
                    Return True
                Case Else
                    Return False
            End Select
        End Function
    End Module
End Namespace
