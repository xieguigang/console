Imports System.Threading
Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.Language.UnixBash

Public Class Form1

    Dim WithEvents console As Microsoft.VisualBasic.Windows.Forms.Console
    Dim shell As Shell

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles Me.Load
        console = ConsoleControl1.Console
        console.ForegroundColor = ConsoleColor.White
        console.SetConsoleBackColor(Color.DodgerBlue)

        console.Write("Microsoft Windows [Version 10.0.19041.508]
(c) 2020 Microsoft Corporation. All rights reserved.

# ")

        shell = New Shell(PS1.Fedora12, Sub(code)
                                            Call console.WriteLine($"running [{code}] job done!")
                                        End Sub, console)
        Call New Thread(AddressOf shell.Run).Start()
    End Sub

    Private Sub console_CancelKeyPress() Handles console.CancelKeyPress
        Dim tmp = console.ForegroundColor

        console.ForegroundColor = ConsoleColor.Magenta
        console.WriteLine("task has been cancel...")
        console.ForegroundColor = tmp
    End Sub
End Class
