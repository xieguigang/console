Imports System

Namespace lsd

    Module Program

        Private Const Version As String = "lsd 1.0.0"

    Private ReadOnly HelpText As String =
"Usage: lsd [OPTION]... [FILE]..." & vbCrLf & vbCrLf &
"List information about the FILEs (the current directory by default)." & vbCrLf &
"Colorful, icon enhanced replacement for bash ls with a tree mode." & vbCrLf & vbCrLf &
"Options:" & vbCrLf &
"  -a, --all            do not ignore entries starting with ." & vbCrLf &
"  -A, --almost-all     ignore . and .. but list other hidden entries" & vbCrLf &
"  -l, --long           use a long listing format" & vbCrLf &
"  -t                   sort by modification time, newest first" & vbCrLf &
"  -S                   sort by file size, largest first" & vbCrLf &
"  -r, --reverse        reverse order while sorting" & vbCrLf &
"  -R, --recursive      list subdirectories recursively" & vbCrLf &
"  -T, --tree           display the directory as a file tree" & vbCrLf &
"      --depth=N        limit tree recursion depth to N (tree mode)" & vbCrLf &
"      --color=WHEN     colorize the output; WHEN is auto, always or never" & vbCrLf &
"      --help           display this help and exit" & vbCrLf &
"  -v, --version        output version information and exit" & vbCrLf & vbCrLf &
"Examples:" & vbCrLf &
"  lsd                  list current directory with icons and colors" & vbCrLf &
"  lsd -l               long format listing" & vbCrLf &
"  lsd -T ./            show a file tree of the current folder" & vbCrLf &
"  lsd -T -depth=2 ./   tree limited to depth 2"

    Sub Main(args As String())
        Dim opts = ArgsParser.Parse(args)

        If opts.ShowHelp Then
            Console.WriteLine(HelpText)
            Return
        End If

        If opts.ShowVersion Then
            Console.WriteLine(Version)
            Return
        End If

        Try
            If opts.TreeMode Then
                TreeRenderer.Render(opts)
            Else
                LsRenderer.Render(opts)
            End If
        Catch ex As Exception
            Console.Error.WriteLine($"lsd: {ex.Message}")
            Environment.ExitCode = 1
        End Try
    End Sub
    End Module
End Namespace
