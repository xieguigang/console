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
        richTextBoxConsole = New System.Windows.Forms.RichTextBox()
        SuspendLayout()
        ' 
        ' richTextBoxConsole
        ' 
        richTextBoxConsole.AcceptsTab = True
        richTextBoxConsole.BackColor = Drawing.Color.Black
        richTextBoxConsole.Dock = System.Windows.Forms.DockStyle.Fill
        richTextBoxConsole.Font = New Drawing.Font("Consolas", 12.0F, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point, 0)
        richTextBoxConsole.ForeColor = Drawing.Color.White
        richTextBoxConsole.Location = New Drawing.Point(0, 0)
        richTextBoxConsole.Name = "richTextBoxConsole"
        richTextBoxConsole.ReadOnly = True
        richTextBoxConsole.Size = New Drawing.Size(150, 150)
        richTextBoxConsole.TabIndex = 0
        richTextBoxConsole.Text = ""
        ' 
        ' ConsoleControl
        ' 
        AutoScaleDimensions = New Drawing.SizeF(6.0F, 13.0F)
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Controls.Add(richTextBoxConsole)
        Name = "ConsoleControl"
        ResumeLayout(False)

    End Sub

#End Region

    Private richTextBoxConsole As System.Windows.Forms.RichTextBox
End Class
