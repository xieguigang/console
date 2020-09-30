Imports System.Threading

Public Class Form1

    Dim WithEvents console As Microsoft.VisualBasic.Windows.Forms.Console


    Private Sub ConsoleControl1_DoubleClick(sender As Object, e As EventArgs) Handles ConsoleControl1.DoubleClick

    End Sub

    Private Sub ReadLineToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ReadLineToolStripMenuItem.Click
        Call New Thread(Sub()
                            console.WriteLine("Your input is: " & console.ReadLine)
                        End Sub).Start()
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles Me.Load
        console = ConsoleControl1.Console
        console.ForegroundColor = ConsoleColor.White
        console.SetConsoleBackColor(Color.DodgerBlue)

        console.Write("Microsoft Windows [Version 10.0.19041.508]
(c) 2020 Microsoft Corporation. All rights reserved.

# ")
    End Sub

    Private Sub ReadCharToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ReadCharToolStripMenuItem.Click
        Call New Thread(Sub()
                            MsgBox(console.ReadKey)
                        End Sub).Start()
    End Sub

    Private Sub console_CancelKeyPress() Handles console.CancelKeyPress
        Dim tmp = console.ForegroundColor

        console.ForegroundColor = ConsoleColor.Magenta
        console.WriteLine("task has been cancel...")
        console.ForegroundColor = tmp
    End Sub
End Class
