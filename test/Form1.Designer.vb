<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
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
        Me.components = New System.ComponentModel.Container()
        Me.ConsoleControl1 = New Microsoft.VisualBasic.Windows.Forms.ConsoleControl()
        Me.ContextMenuStrip1 = New System.Windows.Forms.ContextMenuStrip(Me.components)
        Me.ReadLineToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ReadCharToolStripMenuItem = New System.Windows.Forms.ToolStripMenuItem()
        Me.ContextMenuStrip1.SuspendLayout()
        Me.SuspendLayout()
        '
        'ConsoleControl1
        '
        Me.ConsoleControl1.BackColor = System.Drawing.Color.Black
        Me.ConsoleControl1.BorderStyle = System.Windows.Forms.BorderStyle.None
        Me.ConsoleControl1.ContextMenuStrip = Me.ContextMenuStrip1
        Me.ConsoleControl1.Dock = System.Windows.Forms.DockStyle.Fill
        Me.ConsoleControl1.Font = New System.Drawing.Font("Consolas", 12.0!, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, CType(0, Byte))
        Me.ConsoleControl1.ForeColor = System.Drawing.Color.White
        Me.ConsoleControl1.Location = New System.Drawing.Point(0, 0)
        Me.ConsoleControl1.Name = "ConsoleControl1"
        Me.ConsoleControl1.ReadOnly = True
        Me.ConsoleControl1.Size = New System.Drawing.Size(1061, 601)
        Me.ConsoleControl1.TabIndex = 0
        Me.ConsoleControl1.Text = ""
        '
        'ContextMenuStrip1
        '
        Me.ContextMenuStrip1.Items.AddRange(New System.Windows.Forms.ToolStripItem() {Me.ReadLineToolStripMenuItem, Me.ReadCharToolStripMenuItem})
        Me.ContextMenuStrip1.Name = "ContextMenuStrip1"
        Me.ContextMenuStrip1.Size = New System.Drawing.Size(181, 70)
        '
        'ReadLineToolStripMenuItem
        '
        Me.ReadLineToolStripMenuItem.Name = "ReadLineToolStripMenuItem"
        Me.ReadLineToolStripMenuItem.Size = New System.Drawing.Size(180, 22)
        Me.ReadLineToolStripMenuItem.Text = "ReadLine"
        '
        'ReadCharToolStripMenuItem
        '
        Me.ReadCharToolStripMenuItem.Name = "ReadCharToolStripMenuItem"
        Me.ReadCharToolStripMenuItem.Size = New System.Drawing.Size(180, 22)
        Me.ReadCharToolStripMenuItem.Text = "ReadChar"
        '
        'Form1
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(6.0!, 13.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.ClientSize = New System.Drawing.Size(1061, 601)
        Me.Controls.Add(Me.ConsoleControl1)
        Me.Name = "Form1"
        Me.Text = "Form1"
        Me.ContextMenuStrip1.ResumeLayout(False)
        Me.ResumeLayout(False)

    End Sub

    Friend WithEvents ConsoleControl1 As Microsoft.VisualBasic.Windows.Forms.ConsoleControl
    Friend WithEvents ContextMenuStrip1 As ContextMenuStrip
    Friend WithEvents ReadLineToolStripMenuItem As ToolStripMenuItem
    Friend WithEvents ReadCharToolStripMenuItem As ToolStripMenuItem
End Class
