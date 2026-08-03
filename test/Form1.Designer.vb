Imports System.Windows.Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class Form1
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
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
        components = New System.ComponentModel.Container()
        ContextMenuStrip1 = New ContextMenuStrip(components)
        ReadLineToolStripMenuItem = New ToolStripMenuItem()
        ReadCharToolStripMenuItem = New ToolStripMenuItem()
        ConsoleControl1 = New Microsoft.VisualBasic.Windows.Forms.WebViewConsole()
        ContextMenuStrip1.SuspendLayout()
        SuspendLayout()
        ' 
        ' ContextMenuStrip1
        ' 
        ContextMenuStrip1.Items.AddRange(New ToolStripItem() {ReadLineToolStripMenuItem, ReadCharToolStripMenuItem})
        ContextMenuStrip1.Name = "ContextMenuStrip1"
        ContextMenuStrip1.Size = New System.Drawing.Size(126, 48)
        ' 
        ' ReadLineToolStripMenuItem
        ' 
        ReadLineToolStripMenuItem.Name = "ReadLineToolStripMenuItem"
        ReadLineToolStripMenuItem.Size = New System.Drawing.Size(125, 22)
        ReadLineToolStripMenuItem.Text = "ReadLine"
        ' 
        ' ReadCharToolStripMenuItem
        ' 
        ReadCharToolStripMenuItem.Name = "ReadCharToolStripMenuItem"
        ReadCharToolStripMenuItem.Size = New System.Drawing.Size(125, 22)
        ReadCharToolStripMenuItem.Text = "ReadChar"
        ' 
        ' WebViewConsole1
        ' 
        ConsoleControl1.Dock = DockStyle.Fill
        ConsoleControl1.Location = New System.Drawing.Point(0, 0)
        ConsoleControl1.Name = "WebViewConsole1"
        ConsoleControl1.Size = New System.Drawing.Size(677, 447)
        ConsoleControl1.TabIndex = 1
        ' 
        ' Form1
        ' 
        AutoScaleDimensions = New System.Drawing.SizeF(7.0F, 15.0F)
        AutoScaleMode = AutoScaleMode.Font
        ClientSize = New System.Drawing.Size(677, 447)
        Controls.Add(ConsoleControl1)
        Margin = New Padding(4, 3, 4, 3)
        Name = "Form1"
        Text = "Form1"
        ContextMenuStrip1.ResumeLayout(False)
        ResumeLayout(False)

    End Sub
    Friend WithEvents ContextMenuStrip1 As ContextMenuStrip
    Friend WithEvents ReadLineToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents ReadCharToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents ConsoleControl1 As Microsoft.VisualBasic.Windows.Forms.WebViewConsole
End Class
