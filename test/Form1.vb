Public Class Form1

    Dim console As Microsoft.VisualBasic.Windows.Forms.Console


    Private Sub ConsoleControl1_DoubleClick(sender As Object, e As EventArgs) Handles ConsoleControl1.DoubleClick

    End Sub

    Private Sub ReadLineToolStripMenuItem_Click(sender As Object, e As EventArgs) Handles ReadLineToolStripMenuItem.Click
        MsgBox(console.ReadLine)
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles Me.Load
        console = ConsoleControl1.Console
    End Sub
End Class
