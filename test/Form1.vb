Imports System.ComponentModel

Public Class Form1

    Dim WithEvents console As Microsoft.VisualBasic.Windows.Forms.Console

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles Me.Load
        console = ConsoleControl1.Console
        console.ForegroundColor = ConsoleColor.White
        console.SetConsoleBackColor(Color.DodgerBlue)

        Call DemoInterpreter.Start(console)
    End Sub

    Private Sub console_CancelKeyPress() Handles console.CancelKeyPress
        Dim tmp = console.ForegroundColor

        console.ForegroundColor = ConsoleColor.Magenta
        console.WriteLine("task has been cancel...")
        console.ForegroundColor = tmp
    End Sub

    Private Sub Form1_Closed(sender As Object, e As EventArgs) Handles Me.Closed
        App.Exit()
    End Sub
End Class
