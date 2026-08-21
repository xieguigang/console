Imports System
Imports System.IO
Imports System.Text
Imports Microsoft.VisualBasic.ApplicationServices.Terminal

Namespace lsd

    ''' <summary>
    ''' Renders the directory contents as an ascii tree, compatible with the
    ''' linux <c>tree</c> command. The <c>-depth=N</c> option limits the
    ''' recursion depth.
    ''' </summary>
    Public Module TreeRenderer

        Private Const Branch As String = "├── " ' ├──
        Private Const LastBranch As String = "└── " ' └──
        Private Const Pipe As String = "│   " ' │
        Private Const SpaceIndent As String = "    "

        ''' <summary>
        ''' Render the tree for every requested path.
        ''' </summary>
        Public Sub Render(opts As LsdOptions)
            For Each path As String In opts.Paths
                If Not Directory.Exists(path) Then
                    Console.Error.WriteLine($"lsd: cannot access '{path}': No such file or directory")
                    Continue For
                End If

                Dim rootName = If(path = ".", Environment.CurrentDirectory, path)
                Dim rootSb As New StringBuilder()
                ConsoleColorTheme.AppendStyledName(rootSb, New FileEntry With {
                    .Name = rootName,
                    .IsDirectory = True
                }, opts.ColorEnabled)
                Console.WriteLine(rootSb.ToString())

                Dim entries = FileSystemScanner.ListDirectory(path, opts)
                LsRenderer.SortEntries(entries, opts)
                RenderLevel(entries, "", opts, 1)
            Next
        End Sub

        Private Sub RenderLevel(entries As List(Of FileEntry), indent As String, opts As LsdOptions, depth As Integer)
            For i As Integer = 0 To entries.Count - 1
                Dim entry = entries(i)
                Dim isLast = (i = entries.Count - 1)
                Dim prefix = If(isLast, LastBranch, Branch)

                Dim line As New StringBuilder()
                line.Append(indent)
                line.Append(prefix)

                Dim nameSb As New StringBuilder()
                ConsoleColorTheme.AppendStyledName(nameSb, entry, opts.ColorEnabled)
                line.Append(nameSb.ToString())
                Console.WriteLine(line.ToString())

                If entry.IsDirectory AndAlso (opts.Depth < 0 OrElse depth < opts.Depth) Then
                    Dim childIndent = indent & If(isLast, SpaceIndent, Pipe)
                    Dim children = FileSystemScanner.ListDirectory(entry.FullPath, opts)
                    LsRenderer.SortEntries(children, opts)
                    RenderLevel(children, childIndent, opts, depth + 1)
                End If
            Next
        End Sub
    End Module
End Namespace
