Imports System.Windows.Forms

''' <summary>
''' A minimal demo host for <see cref="SshWinFormConsole"/>: a small connection
''' bar (host / user / password / port and a Connect button) plus the console.
''' </summary>
Public Class MainForm : Inherits Form

        Public Sub New()
            Call InitializeComponent()
        End Sub

        Private Sub ConnectButton_Click(sender As Object, e As EventArgs) Handles ConnectButton.Click
            If String.IsNullOrWhiteSpace(HostTextBox.Text) OrElse String.IsNullOrWhiteSpace(UserTextBox.Text) Then
                MessageBox.Show("Host and user name are required.", "SSH", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                Return
            End If

            SshConsole.ConnectionOptions = New SshConnectionOptions() With {
                .Host = HostTextBox.Text,
                .Port = If(Integer.TryParse(PortTextBox.Text, Nothing), CInt(PortTextBox.Text), 22),
                .UserName = UserTextBox.Text,
                .Password = PasswordTextBox.Text
            }

            SshConsole.Connect()
            SshConsole.Focus()
        End Sub

        Private Sub DisconnectButton_Click(sender As Object, e As EventArgs) Handles DisconnectButton.Click
            SshConsole.Disconnect()
        End Sub
    End Class
