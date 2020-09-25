Imports System.Threading

Public Class Form1

    Dim console As Microsoft.VisualBasic.Windows.Forms.Console


    Private Sub ConsoleControl1_DoubleClick(sender As Object, e As EventArgs) Handles ConsoleControl1.DoubleClick

    End Sub

    Private Sub ReadLineToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ReadLineToolStripMenuItem.Click
        Call New Thread(Sub()
                            MsgBox(console.ReadLine)
                        End Sub).Start()
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles Me.Load
        console = ConsoleControl1.Console
    End Sub

    Private Sub ReadCharToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ReadCharToolStripMenuItem.Click
        Call New Thread(Sub()
                            MsgBox(console.ReadKey)
                        End Sub).Start()
    End Sub

    Private Sub Form1_Resize(sender As Object, e As EventArgs) Handles Me.Resize
        If Not console Is Nothing Then
            console.Write("Z"c)
        End If
    End Sub
End Class
