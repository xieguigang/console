﻿<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class ConsoleControl
    Inherits System.Windows.Forms.RichTextBox

    'UserControl1 overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        Me.SuspendLayout()
        '
        'RichTextBox1
        '

        '
        'ConsoleControl
        '

        Me.BackColor = System.Drawing.Color.Black
        Me.ForeColor = System.Drawing.Color.White
        Me.Name = "ConsoleControl"
        Me.Size = New System.Drawing.Size(800, 450)
        Me.ResumeLayout(False)

    End Sub

End Class
