Imports System.IO

Public Class ConsoleControl

    Public ReadOnly Property Console As Console

    Dim m_device As StreamWriter

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        Call ConsoleControl_Load()
    End Sub

    Private Sub RichTextBox1_KeyPress(sender As Object, e As KeyPressEventArgs) Handles Me.KeyPress
        e.Handled = True

        If e.KeyChar = vbCr Then
            Me.AppendText(e.KeyChar)
            Return
        End If

        Dim cursor = Me.SelectionStart
        Me.Select(Me.TextLength, 0)
        Dim lastFirst = Me.GetFirstCharIndexOfCurrentLine

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
                Me.AppendText(e.KeyChar)
            End If
        End If
    End Sub

    Private Sub ConsoleControl_Load()
        Me.Text = ""
        Me.ReadOnly = True

        _Console = New Console(Me)
        m_device = Console.OpenInput
    End Sub

    Private Sub insertChar(cursor As Integer, c As Char)
        Me.ReadOnly = False
        Me.Select(cursor, 0)
        Me.SelectedText = c
        Me.ReadOnly = True
    End Sub

    Private Sub deleteChar(cursor As Integer)
        Me.ReadOnly = False
        Me.Select(cursor, 1)
        Me.SelectedText = ""
        Me.ReadOnly = True
    End Sub

    Private Sub RichTextBox1_KeyUp(sender As Object, e As KeyEventArgs) Handles Me.KeyUp
        Dim cursor = Me.SelectionStart

        e.Handled = True

        If e.KeyCode = Keys.Left Then
            If cursor > 0 Then
                Me.Select(cursor - 1, 0)
            End If
        ElseIf e.KeyCode = Keys.Right Then
            If cursor < Me.TextLength Then
                Me.Select(cursor + 1, 0)
            End If
        ElseIf e.KeyCode = Keys.Delete Then
            Me.Select(Me.TextLength, 0)
            Dim lastFirst = Me.GetFirstCharIndexOfCurrentLine

            If cursor > lastFirst Then
                deleteChar(cursor)
            Else
                ' can not delete
                e.Handled = False
            End If
        End If
    End Sub

    Private Sub RichTextBox1_KeyDown(sender As Object, e As KeyEventArgs) Handles Me.KeyDown
        e.Handled = True
    End Sub
End Class
