Imports System
Imports System.Collections.Generic

Namespace lsd

    ''' <summary>
    ''' Parsed command line options for the <c>lsd</c> program.
    ''' </summary>
    Public Class LsdOptions

        ''' <summary>-a: do not ignore entries starting with . (includes . and ..)</summary>
        Public ShowAll As Boolean = False

        ''' <summary>-A: almost all; ignore . and .. but show other hidden entries</summary>
        Public AlmostAll As Boolean = False

        ''' <summary>-l: use a long listing format</summary>
        Public LongFormat As Boolean = False

        ''' <summary>-t: tree mode (display folder as a file tree)</summary>
        Public TreeMode As Boolean = False

        ''' <summary>-t (ls sort): sort by modification time, newest first</summary>
        Public SortByTime As Boolean = False

        ''' <summary>-S: sort by file size, largest first</summary>
        Public SortBySize As Boolean = False

        ''' <summary>-r: reverse order while sorting</summary>
        Public Reverse As Boolean = False

        ''' <summary>-R: list subdirectories recursively</summary>
        Public Recursive As Boolean = False

        ''' <summary>--color=auto/always/never</summary>
        Public ColorMode As String = "auto"

        ''' <summary>-depth=N: tree recursion depth, -1 means unlimited</summary>
        Public Depth As Integer = -1

        ''' <summary>positional path arguments; defaults to current directory</summary>
        Public Paths As New List(Of String)

        ''' <summary>requested help output</summary>
        Public ShowHelp As Boolean = False

        ''' <summary>requested version output</summary>
        Public ShowVersion As Boolean = False

        ''' <summary>resolve the effective color mode flag; auto disables color when output is redirected.</summary>
        Public ReadOnly Property ColorEnabled As Boolean
            Get
                Select Case ColorMode.ToLowerInvariant()
                    Case "never"
                        Return False
                    Case "always"
                        Return True
                    Case Else ' auto
                        Return Not Console.IsOutputRedirected
                End Select
            End Get
        End Property
    End Class
End Namespace
