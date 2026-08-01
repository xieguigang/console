Imports System.Windows.Forms

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
        Private SshConsole As SshWinFormConsole

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If

            MyBase.Dispose(disposing)
        End Sub

        Private Sub InitializeComponent()
            Me.HostLabel = New Label()
            Me.HostTextBox = New TextBox()
            Me.UserLabel = New Label()
            Me.UserTextBox = New TextBox()
            Me.PasswordLabel = New Label()
            Me.PasswordTextBox = New TextBox()
            Me.PortLabel = New Label()
            Me.PortTextBox = New TextBox()
            Me.ConnectButton = New Button()
            Me.DisconnectButton = New Button()
            Me.ToolStrip = New Panel()
            Me.SshConsole = New SshWinFormConsole()
            Me.SuspendLayout()

            '  ToolStrip
            Me.ToolStrip.Dock = DockStyle.Top
            Me.ToolStrip.Height = 32
            Me.ToolStrip.BackColor = System.Drawing.SystemColors.Control

            '  Host
            Me.HostLabel.Text = "Host"
            Me.HostLabel.AutoSize = True
            Me.HostLabel.Location = New System.Drawing.Point(6, 9)
            Me.HostTextBox.Location = New System.Drawing.Point(44, 6)
            Me.HostTextBox.Size = New System.Drawing.Size(160, 23)
            Me.HostTextBox.Text = ""

            '  User
            Me.UserLabel.Text = "User"
            Me.UserLabel.AutoSize = True
            Me.UserLabel.Location = New System.Drawing.Point(214, 9)
            Me.UserTextBox.Location = New System.Drawing.Point(248, 6)
            Me.UserTextBox.Size = New System.Drawing.Size(100, 23)
            Me.UserTextBox.Text = ""

            '  Password
            Me.PasswordLabel.Text = "Password"
            Me.PasswordLabel.AutoSize = True
            Me.PasswordLabel.Location = New System.Drawing.Point(358, 9)
            Me.PasswordTextBox.Location = New System.Drawing.Point(420, 6)
            Me.PasswordTextBox.Size = New System.Drawing.Size(100, 23)
            Me.PasswordTextBox.UseSystemPasswordChar = True
            Me.PasswordTextBox.Text = ""

            '  Port
            Me.PortLabel.Text = "Port"
            Me.PortLabel.AutoSize = True
            Me.PortLabel.Location = New System.Drawing.Point(530, 9)
            Me.PortTextBox.Location = New System.Drawing.Point(566, 6)
            Me.PortTextBox.Size = New System.Drawing.Size(50, 23)
            Me.PortTextBox.Text = "22"

            '  Connect / Disconnect
            Me.ConnectButton.Text = "Connect"
            Me.ConnectButton.Location = New System.Drawing.Point(626, 5)
            Me.ConnectButton.Size = New System.Drawing.Size(75, 23)

            Me.DisconnectButton.Text = "Disconnect"
            Me.DisconnectButton.Location = New System.Drawing.Point(707, 5)
            Me.DisconnectButton.Size = New System.Drawing.Size(75, 23)

            Me.ToolStrip.Controls.AddRange(New Control() {
                Me.HostLabel, Me.HostTextBox,
                Me.UserLabel, Me.UserTextBox,
                Me.PasswordLabel, Me.PasswordTextBox,
                Me.PortLabel, Me.PortTextBox,
                Me.ConnectButton, Me.DisconnectButton})

            '  SshConsole
            Me.SshConsole.Dock = DockStyle.Fill
            Me.SshConsole.Location = New System.Drawing.Point(0, 32)
            Me.SshConsole.Name = "SshConsole"
            Me.SshConsole.Size = New System.Drawing.Size(800, 468)
            Me.SshConsole.TabIndex = 0

            '  MainForm
            Me.ClientSize = New System.Drawing.Size(800, 500)
            Me.Controls.Add(Me.SshConsole)
            Me.Controls.Add(Me.ToolStrip)
            Me.Name = "MainForm"
            Me.Text = "SSH Console"
            Me.ResumeLayout(False)
            Me.PerformLayout()
        End Sub
    End Class
