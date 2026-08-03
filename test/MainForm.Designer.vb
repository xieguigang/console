Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.VisualBasic.Windows.Forms.SshClient

Partial Class MainForm

    Private components As System.ComponentModel.IContainer = Nothing

    Private HostLabel As Label
    Private HostTextBox As TextBox
    Private UserLabel As Label
    Private UserTextBox As TextBox
    Private PasswordLabel As Label
    Private PasswordTextBox As TextBox
    Private PortLabel As Label
    Private PortTextBox As TextBox
    Private WithEvents ConnectButton As Button
    Private WithEvents DisconnectButton As Button
    Private ToolStrip As Panel

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing AndAlso components IsNot Nothing Then
            components.Dispose()
        End If

        MyBase.Dispose(disposing)
    End Sub

    Private Sub InitializeComponent()
        Dim SshConnectionOptions1 As SshConnectionOptions = New SshConnectionOptions()
        HostLabel = New Label()
        HostTextBox = New TextBox()
        UserLabel = New Label()
        UserTextBox = New TextBox()
        PasswordLabel = New Label()
        PasswordTextBox = New TextBox()
        PortLabel = New Label()
        PortTextBox = New TextBox()
        ConnectButton = New Button()
        DisconnectButton = New Button()
        ToolStrip = New Panel()
        SshConsole = New SshWinFormConsole()
        ToolStrip.SuspendLayout()
        SuspendLayout()
        ' 
        ' HostLabel
        ' 
        HostLabel.AutoSize = True
        HostLabel.Location = New Point(6, 9)
        HostLabel.Name = "HostLabel"
        HostLabel.Size = New Size(32, 15)
        HostLabel.TabIndex = 0
        HostLabel.Text = "Host"
        ' 
        ' HostTextBox
        ' 
        HostTextBox.Location = New Point(44, 6)
        HostTextBox.Name = "HostTextBox"
        HostTextBox.Size = New Size(160, 23)
        HostTextBox.TabIndex = 1
        HostTextBox.Text = "192.168.3.14"
        ' 
        ' UserLabel
        ' 
        UserLabel.AutoSize = True
        UserLabel.Location = New Point(214, 9)
        UserLabel.Name = "UserLabel"
        UserLabel.Size = New Size(30, 15)
        UserLabel.TabIndex = 2
        UserLabel.Text = "User"
        ' 
        ' UserTextBox
        ' 
        UserTextBox.Location = New Point(248, 6)
        UserTextBox.Name = "UserTextBox"
        UserTextBox.Size = New Size(100, 23)
        UserTextBox.TabIndex = 3
        UserTextBox.Text = "xieguigang"
        ' 
        ' PasswordLabel
        ' 
        PasswordLabel.AutoSize = True
        PasswordLabel.Location = New Point(358, 9)
        PasswordLabel.Name = "PasswordLabel"
        PasswordLabel.Size = New Size(57, 15)
        PasswordLabel.TabIndex = 4
        PasswordLabel.Text = "Password"
        ' 
        ' PasswordTextBox
        ' 
        PasswordTextBox.Location = New Point(420, 6)
        PasswordTextBox.Name = "PasswordTextBox"
        PasswordTextBox.Size = New Size(100, 23)
        PasswordTextBox.TabIndex = 5
        PasswordTextBox.Text = "1"
        PasswordTextBox.UseSystemPasswordChar = True
        ' 
        ' PortLabel
        ' 
        PortLabel.AutoSize = True
        PortLabel.Location = New Point(530, 9)
        PortLabel.Name = "PortLabel"
        PortLabel.Size = New Size(29, 15)
        PortLabel.TabIndex = 6
        PortLabel.Text = "Port"
        ' 
        ' PortTextBox
        ' 
        PortTextBox.Location = New Point(566, 6)
        PortTextBox.Name = "PortTextBox"
        PortTextBox.Size = New Size(50, 23)
        PortTextBox.TabIndex = 7
        PortTextBox.Text = "22"
        ' 
        ' ConnectButton
        ' 
        ConnectButton.Location = New Point(626, 5)
        ConnectButton.Name = "ConnectButton"
        ConnectButton.Size = New Size(75, 23)
        ConnectButton.TabIndex = 8
        ConnectButton.Text = "Connect"
        ' 
        ' DisconnectButton
        ' 
        DisconnectButton.Location = New Point(707, 5)
        DisconnectButton.Name = "DisconnectButton"
        DisconnectButton.Size = New Size(75, 23)
        DisconnectButton.TabIndex = 9
        DisconnectButton.Text = "Disconnect"
        ' 
        ' ToolStrip
        ' 
        ToolStrip.BackColor = SystemColors.Control
        ToolStrip.Controls.Add(HostLabel)
        ToolStrip.Controls.Add(HostTextBox)
        ToolStrip.Controls.Add(UserLabel)
        ToolStrip.Controls.Add(UserTextBox)
        ToolStrip.Controls.Add(PasswordLabel)
        ToolStrip.Controls.Add(PasswordTextBox)
        ToolStrip.Controls.Add(PortLabel)
        ToolStrip.Controls.Add(PortTextBox)
        ToolStrip.Controls.Add(ConnectButton)
        ToolStrip.Controls.Add(DisconnectButton)
        ToolStrip.Dock = DockStyle.Top
        ToolStrip.Location = New Point(0, 0)
        ToolStrip.Name = "ToolStrip"
        ToolStrip.Size = New Size(800, 32)
        ToolStrip.TabIndex = 1
        ' 
        ' SshConsole
        ' 
        SshConsole.ConnectionOptions = SshConnectionOptions1
        SshConsole.Dock = DockStyle.Fill
        SshConsole.Host = ""
        SshConsole.Location = New Point(0, 32)
        SshConsole.Margin = New Padding(5)
        SshConsole.Name = "SshConsole"
        SshConsole.Password = ""
        SshConsole.Size = New Size(800, 468)
        SshConsole.TabIndex = 0
        SshConsole.UserName = ""
        ' 
        ' MainForm
        ' 
        ClientSize = New Size(800, 500)
        Controls.Add(SshConsole)
        Controls.Add(ToolStrip)
        Name = "MainForm"
        Text = "SSH Console"
        ToolStrip.ResumeLayout(False)
        ToolStrip.PerformLayout()
        ResumeLayout(False)
    End Sub

    Private WithEvents SshConsole As SshWinFormConsole
End Class
