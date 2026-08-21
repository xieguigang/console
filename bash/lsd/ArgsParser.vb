Imports System
Imports System.Collections.Generic
Imports System.Text.RegularExpressions

Namespace lsd

    ''' <summary>
    ''' Lightweight command line parser for <c>lsd</c>. Supports short
    ''' option clustering (e.g. <c>-alR</c>), long options with equals
    ''' assignment (e.g. <c>--color=never</c>, <c>-depth=2</c>) and a
    ''' list of positional path arguments.
    ''' </summary>
    Public Module ArgsParser

        Private ReadOnly depthPattern As New Regex("^-depth=(\d+)$", RegexOptions.IgnoreCase)

        ''' <summary>
        ''' Parse the given argument array into a <see cref="LsdOptions"/> instance.
        ''' </summary>
        Public Function Parse(args As String()) As LsdOptions
            Dim opts As New LsdOptions()

            For Each arg As String In args
                If String.IsNullOrEmpty(arg) Then
                    Continue For
                End If

                If arg = "--help" OrElse arg = "-h" Then
                    opts.ShowHelp = True
                    Continue For
                End If

                If arg = "--version" OrElse arg = "-v" Then
                    opts.ShowVersion = True
                    Continue For
                End If

                If arg.StartsWith("--") Then
                    ParseLongOption(arg, opts)
                    Continue For
                End If

                If arg.StartsWith("-") AndAlso arg.Length > 1 Then
                    ParseShortOptions(arg, opts)
                    Continue For
                End If

                ' positional path argument
                opts.Paths.Add(arg)
            Next

            If opts.Paths.Count = 0 Then
                opts.Paths.Add(".")
            End If

            ' -t is overloaded: in tree mode it means tree, otherwise it is
            ' the time sort flag. The tree flag takes precedence when set.
            Return opts
        End Function

        Private Sub ParseLongOption(arg As String, opts As LsdOptions)
            Dim body = arg.Substring(2)
            Dim eq = body.IndexOf("="c)

            If eq >= 0 Then
                Dim name = body.Substring(0, eq)
                Dim value = body.Substring(eq + 1)

                Select Case name.ToLowerInvariant()
                    Case "color"
                        opts.ColorMode = value
                    Case "depth"
                        Dim d As Integer
                        If Integer.TryParse(value, d) Then
                            opts.Depth = d
                        End If
                    Case Else
                        ' unknown long option with assignment: ignore
                End Select
            Else
                Select Case body.ToLowerInvariant()
                    Case "color"
                        opts.ColorMode = "always"
                    Case "recursive"
                        opts.Recursive = True
                    Case "all"
                        opts.ShowAll = True
                    Case "almost-all"
                        opts.AlmostAll = True
                    Case "long"
                        opts.LongFormat = True
                    Case "tree"
                        opts.TreeMode = True
                    Case "reverse"
                        opts.Reverse = True
                    Case "sort-time"
                        opts.SortByTime = True
                    Case "sort-size"
                        opts.SortBySize = True
                    Case Else
                        ' unknown long option: ignore
                End Select
            End If
        End Sub

        Private Sub ParseShortOptions(arg As String, opts As LsdOptions)
            ' support -depth=2 style on the short dash as well
            Dim m = depthPattern.Match(arg)
            If m.Success Then
                opts.Depth = Integer.Parse(m.Groups(1).Value)
                Return
            End If

            ' cluster of single character flags, e.g. -alR
            For i As Integer = 1 To arg.Length - 1
                Dim c = arg(i)

                Select Case c
                    Case "a"c
                        opts.ShowAll = True
                    Case "A"c
                        opts.AlmostAll = True
                    Case "l"c
                        opts.LongFormat = True
                    Case "t"c
                        opts.SortByTime = True
                    Case "S"c
                        opts.SortBySize = True
                    Case "r"c
                        opts.Reverse = True
                    Case "R"c
                        opts.Recursive = True
                    Case "T"c
                        opts.TreeMode = True
                    Case Else
                        ' unknown short flag: ignore
                End Select
            Next
        End Sub
    End Module
End Namespace
