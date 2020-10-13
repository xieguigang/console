Imports System.Text.RegularExpressions
Imports Microsoft.VisualBasic.Text
Imports keys = System.Windows.Forms.Keys

Public Class ConsoleControl

    Public ReadOnly Property Console As Console

    Friend background As Color = Color.Black
    Friend foreground As Color = Color.White
    Friend ps1 As Regex

    Public ReadOnly Property LastLine As String
        Get
            Return getLastLineRaw(offset:=getPs1StringLength)
        End Get
    End Property

    Public Property Ps1Pattern As String
        Get
            If ps1 Is Nothing Then
                Return ""
            Else
                Return ps1.ToString
            End If
        End Get
        Set(value As String)
            ps1 = New Regex(value)
        End Set
    End Property

    Public Sub New()

        ' This call is required by the designer.
        InitializeComponent()

        ' Add any initialization after the InitializeComponent() call.
        Call ConsoleControl_Load()
    End Sub

    Private Function getLastLineRaw(offset As Integer) As String
        Dim cursor = Me.SelectionStart
        Me.Select(Me.TextLength, 0)
        Dim lastFirst = Me.GetFirstCharIndexOfCurrentLine
        Me.Select(lastFirst + offset, Me.TextLength - lastFirst - offset)
        Dim last As String = Me.SelectedText
        Me.Select(cursor, 0)
        Return last
    End Function

    Private Function getPs1StringLength() As Integer
        If Ps1Pattern.StringEmpty(False) Then
            Return 0
        Else
            Dim lastFirst = Me.GetFirstCharIndexOfCurrentLine
            Dim ps1String = ps1.Match(getLastLineRaw(0))

            Return ps1String.Length
        End If
    End Function

    Private Sub RichTextBox1_KeyPress(sender As Object, e As KeyPressEventArgs) Handles Me.KeyPress
        e.Handled = True

        If e.KeyChar = vbCr Then
            Me.Console.sharedStream.Commit(LastLine)
            Me.Console.sharedStream.Push(ASCII.CR)
            Me.AppendText(vbLf)
            Return
        ElseIf e.KeyChar = ASCII.ETX Then
            Call Console.TriggerCancelKeyPress()
            Return
        End If

        Dim cursor = Me.SelectionStart
        Me.Select(Me.TextLength, 0)
        Dim lastFirst = Me.GetFirstCharIndexOfCurrentLine + getPs1StringLength()

        If e.KeyChar = vbBack Then
            If cursor > lastFirst Then
                deleteChar(cursor - 1)
            Else
                ' can not delete
                e.Handled = False
            End If

        Else
            Console.sharedStream.Push(e.KeyChar)

            If cursor > lastFirst Then
                insertChar(cursor, e.KeyChar)
            Else
                Me.AppendText(e.KeyChar)
                Me.Select(Me.TextLength, 1)
                Me.SelectionBackColor = background
                Me.SelectionColor = foreground
            End If
        End If
    End Sub

    Private Sub ConsoleControl_Load()
        Me.Text = ""
        Me.ReadOnly = True
        Me.BackColor = System.Drawing.Color.Black
        Me.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.Dock = System.Windows.Forms.DockStyle.Fill
        Me.Font = New System.Drawing.Font("Consolas", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.ForeColor = System.Drawing.Color.Lime
        Me.Location = New System.Drawing.Point(0, 0)
        Me.Name = "Console1"
        Me.Size = New System.Drawing.Size(800, 450)
        Me.TabIndex = 0
        Me.Text = ""

        _Console = New Console(Me)
    End Sub

    Private Sub insertChar(cursor As Integer, c As Char)
        Me.ReadOnly = False
        Me.Select(cursor, 0)
        Me.SelectedText = c
        Me.Select(cursor, 1)
        Me.SelectionBackColor = background
        Me.SelectionColor = foreground
        Me.Select(cursor + 1, 0)
        Me.ReadOnly = True
    End Sub

    Friend Sub write(c As String)
        If c.StringEmpty(False) Then
            Return
        End If

        Dim cursor = Me.SelectionStart

        Me.ReadOnly = False
        Me.Select(cursor, c.Length)
        Me.SelectedText = c
        Me.SelectionBackColor = background
        Me.SelectionColor = foreground
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

        If e.KeyCode = keys.Left Then
            If cursor > 0 Then
                Me.Select(cursor - 1, 0)
            End If
        ElseIf e.KeyCode = keys.Right Then
            If cursor < Me.TextLength Then
                Me.Select(cursor + 1, 0)
            End If
        ElseIf e.KeyCode = keys.Delete Then
            Me.Select(Me.TextLength, 0)
            Dim lastFirst = Me.GetFirstCharIndexOfCurrentLine + getPs1StringLength()

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

    Private Sub ConsoleControl_SelectionChanged(sender As Object, e As EventArgs) Handles Me.SelectionChanged

    End Sub
End Class
