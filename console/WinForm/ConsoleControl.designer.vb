Imports System.ComponentModel

Partial Class ConsoleControl
    ''' <summary> 
    ''' Required designer variable.
    ''' </summary>
    Private components As IContainer = Nothing

    ''' <summary> 
    ''' Clean up any resources being used.
    ''' </summary>
    ''' <paramname="disposing">true if managed resources should be disposed; otherwise, false.</param>
    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso components IsNot Nothing Then
            components.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub

#Region "Component Designer generated code"

    ''' <summary> 
    ''' Required method for Designer support - do not modify 
    ''' the contents of this method with the code editor.
    ''' </summary>
    Private Sub InitializeComponent()
        Me.richTextBoxConsole = New System.Windows.Forms.RichTextBox()
        Me.SuspendLayout()
        '
        'richTextBoxConsole
        '
        Me.richTextBoxConsole.AcceptsTab = True
        Me.richTextBoxConsole.BackColor = System.Drawing.Color.Black
        Me.richTextBoxConsole.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.richTextBoxConsole.Dock = System.Windows.Forms.DockStyle.Fill
        Me.richTextBoxConsole.Font = New System.Drawing.Font("Consolas", 9.75!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.richTextBoxConsole.ForeColor = System.Drawing.Color.White
        Me.richTextBoxConsole.Location = New System.Drawing.Point(0, 0)
        Me.richTextBoxConsole.Name = "richTextBoxConsole"
        Me.richTextBoxConsole.ReadOnly = True
        Me.richTextBoxConsole.Size = New System.Drawing.Size(658, 450)
        Me.richTextBoxConsole.TabIndex = 0
        Me.richTextBoxConsole.Text = ""
        '
        'ConsoleControl
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 12.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.Controls.Add(Me.richTextBoxConsole)
        Me.Name = "ConsoleControl"
        Me.Size = New System.Drawing.Size(658, 450)
        Me.ResumeLayout(False)

    End Sub

#End Region

    Private WithEvents richTextBoxConsole As System.Windows.Forms.RichTextBox
End Class
