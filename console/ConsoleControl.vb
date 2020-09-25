Public Class ConsoleControl

    Public ReadOnly Property Console As Console

    Private Sub RichTextBox1_KeyPress(sender As Object, e As KeyPressEventArgs) Handles RichTextBox1.KeyPress
        e.Handled = True

        If e.KeyChar = vbCr Then

        End If

        Dim cursor = RichTextBox1.SelectionStart
        RichTextBox1.Select(RichTextBox1.TextLength, 0)
        Dim lastFirst = RichTextBox1.GetFirstCharIndexOfCurrentLine

        If e.KeyChar = vbBack Then
            If cursor > lastFirst Then
                deleteChar(cursor - 1)
            Else
                ' can not delete
                e.Handled = False
            End If

        Else
            If cursor > lastFirst Then
                insertChar(cursor, e.KeyChar)
            Else
                RichTextBox1.AppendText(e.KeyChar)
            End If
        End If
    End Sub

    Private Sub ConsoleControl_Load(sender As Object, e As EventArgs) Handles Me.Load
        RichTextBox1.Text = ""
        RichTextBox1.ReadOnly = True

        _Console = New Console(RichTextBox1)
    End Sub

    Private Sub insertChar(cursor As Integer, c As Char)
        RichTextBox1.ReadOnly = False
        RichTextBox1.Select(cursor, 0)
        RichTextBox1.SelectedText = c
        RichTextBox1.ReadOnly = True
    End Sub

    Private Sub deleteChar(cursor As Integer)
        RichTextBox1.ReadOnly = False
        RichTextBox1.Select(cursor, 1)
        RichTextBox1.SelectedText = ""
        RichTextBox1.ReadOnly = True
    End Sub

    Private Sub RichTextBox1_KeyUp(sender As Object, e As KeyEventArgs) Handles RichTextBox1.KeyUp
        Dim cursor = RichTextBox1.SelectionStart

        e.Handled = True

        If e.KeyCode = Keys.Left Then
            If cursor > 0 Then
                RichTextBox1.Select(cursor - 1, 0)
            End If
        ElseIf e.KeyCode = Keys.Right Then
            If cursor < RichTextBox1.TextLength Then
                RichTextBox1.Select(cursor + 1, 0)
            End If
        ElseIf e.KeyCode = Keys.Delete Then
            RichTextBox1.Select(RichTextBox1.TextLength, 0)
            Dim lastFirst = RichTextBox1.GetFirstCharIndexOfCurrentLine

            If cursor > lastFirst Then
                deleteChar(cursor)
            Else
                ' can not delete
                e.Handled = False
            End If
        End If
    End Sub

    Private Sub RichTextBox1_KeyDown(sender As Object, e As KeyEventArgs) Handles RichTextBox1.KeyDown
        e.Handled = True
    End Sub
End Class
