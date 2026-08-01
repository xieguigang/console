Imports System.ComponentModel

Namespace SShClient

    ''' <summary>
    ''' Connection parameters for an SSH session. Intended to be populated either
    ''' from a WinForms designer (exposed as properties on <see cref="SshWinFormConsole"/>)
    ''' or programmatically.
    ''' </summary>
    Public Class SshConnectionOptions

        ''' <summary>The remote host name or IP address.</summary>
        <DefaultValue("")>
        Public Property Host As String = ""

        ''' <summary>The SSH port (default 22).</summary>
        <DefaultValue(22)>
        Public Property Port As Integer = 22

        ''' <summary>The user name used to authenticate.</summary>
        <DefaultValue("")>
        Public Property UserName As String = ""

        ''' <summary>The password used for password authentication.</summary>
        <DefaultValue("")>
        Public Property Password As String = ""

        ''' <summary>
        ''' Path to a private key file used for public-key authentication.
        ''' When empty, password authentication is used.
        ''' </summary>
        <DefaultValue("")>
        Public Property PrivateKeyFile As String = ""

        ''' <summary>
        ''' Optional pass-phrase for the private key file.
        ''' </summary>
        <DefaultValue("")>
        Public Property Passphrase As String = ""

        ''' <summary>
        ''' Optional terminal type advertised to the server (default "xterm").
        ''' </summary>
        <DefaultValue("xterm")>
        Public Property TerminalType As String = "xterm"

        ''' <summary>When true, host-key verification is skipped (insecure, for testing only).</summary>
        <DefaultValue(False)>
        Public Property AcceptAnyHostKey As Boolean = False

        ''' <summary>Optional proxy host. Empty means no proxy.</summary>
        <DefaultValue("")>
        Public Property ProxyHost As String = ""

        ''' <summary>Optional proxy port.</summary>
        <DefaultValue(0)>
        Public Property ProxyPort As Integer = 0

        ''' <summary>Optional proxy user name.</summary>
        <DefaultValue("")>
        Public Property ProxyUserName As String = ""

        ''' <summary>Optional proxy password.</summary>
        <DefaultValue("")>
        Public Property ProxyPassword As String = ""

        ''' <summary>
        ''' Returns true when the mandatory fields (host and user name) are present.
        ''' </summary>
        Public Function IsValid() As Boolean
            Return Not String.IsNullOrWhiteSpace(Host) AndAlso Not String.IsNullOrWhiteSpace(UserName)
        End Function
    End Class
End Namespace
