<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class Form2
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
        Dim SshConnectionOptions1 As Microsoft.VisualBasic.Windows.Forms.SshClient.SshConnectionOptions = New Microsoft.VisualBasic.Windows.Forms.SshClient.SshConnectionOptions()
        SshWinFormConsole1 = New Microsoft.VisualBasic.Windows.Forms.SshClient.SshWinFormConsole()
        SuspendLayout()
        ' 
        ' SshWinFormConsole1
        ' 
        SshWinFormConsole1.ConnectionOptions = SshConnectionOptions1
        SshWinFormConsole1.Dock = System.Windows.Forms.DockStyle.Fill
        SshWinFormConsole1.Host = ""
        SshWinFormConsole1.IsInputEnabled = True
        SshWinFormConsole1.Location = New System.Drawing.Point(0, 0)
        SshWinFormConsole1.Name = "SshWinFormConsole1"
        SshWinFormConsole1.Password = ""
        SshWinFormConsole1.ReadOnly = True
        SshWinFormConsole1.SendKeyboardCommandsToProcess = True
        SshWinFormConsole1.ShowDiagnostics = False
        SshWinFormConsole1.Size = New System.Drawing.Size(800, 450)
        SshWinFormConsole1.TabIndex = 0
        SshWinFormConsole1.UserName = ""
        ' 
        ' Form2
        ' 
        AutoScaleDimensions = New System.Drawing.SizeF(7F, 15F)
        AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        ClientSize = New System.Drawing.Size(800, 450)
        Controls.Add(SshWinFormConsole1)
        Name = "Form2"
        Text = "Form2"
        ResumeLayout(False)
    End Sub

    Friend WithEvents SshWinFormConsole1 As Microsoft.VisualBasic.Windows.Forms.SshClient.SshWinFormConsole
End Class
