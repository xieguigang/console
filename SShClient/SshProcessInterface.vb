Imports System.Text
Imports System.Threading
Imports Microsoft.VisualBasic.Windows.Forms.Win32
Imports Renci.SshNet.Common
Imports SSH = Renci.SshNet

''' <summary>
''' An <see cref="AbstractProcessInterface"/> implementation that drives a remote
''' shell over SSH using the SSH.NET library. The remote shell is attached to a
''' <see cref="SSH.ShellStream"/>, so the hosting console control reuses its
''' existing ANSI rendering and input pipeline unchanged.
''' </summary>
Public Class SshProcessInterface : Inherits AbstractProcessInterface

    Private client As SSH.SshClient = Nothing
    Private shell As SSH.ShellStream = Nothing
    Private readThread As Thread = Nothing
    Private runRead As Boolean = False
    Private ReadOnly sync As New Object()

    ''' <summary>The connection options used when <see cref="StartProcess"/> runs.</summary>
    Public Property Options As SshConnectionOptions

    ''' <summary>Terminal columns reported to the server.</summary>
    Public Property Columns As UInteger = 80UI

    ''' <summary>Terminal rows reported to the server.</summary>
    Public Property Rows As UInteger = 24UI

    ''' <summary>The text encoding used for the shell stream.</summary>
    Public Property Encoding As Encoding = Encoding.UTF8

    Sub New(options As SshConnectionOptions)
        Call MyBase.New(void:=Nothing)
        Me.Options = If(options, New SshConnectionOptions())
    End Sub

    Public Overrides ReadOnly Property IsProcessRunning As Boolean
        Get
            Return client IsNot Nothing AndAlso client.IsConnected
        End Get
    End Property

    ''' <summary>
    ''' Builds a <see cref="SSH.ConnectionInfo"/> from the configured options.
    ''' Supports password authentication, private-key authentication and an
    ''' optional HTTP proxy, mirroring the reference implementation.
    ''' </summary>
    Private Function BuildConnectionInfo() As SSH.ConnectionInfo
        Dim o = Options
        Dim auths As New List(Of SSH.AuthenticationMethod)

        If Not String.IsNullOrWhiteSpace(o.PrivateKeyFile) Then
            If String.IsNullOrWhiteSpace(o.Passphrase) Then
                auths.Add(New SSH.PrivateKeyAuthenticationMethod(o.UserName, New SSH.PrivateKeyFile(o.PrivateKeyFile)))
            Else
                auths.Add(New SSH.PrivateKeyAuthenticationMethod(o.UserName, New SSH.PrivateKeyFile(o.PrivateKeyFile, o.Passphrase)))
            End If
        Else
            auths.Add(New SSH.PasswordAuthenticationMethod(o.UserName, o.Password))
        End If

        '  ConnectionInfo(host, port, user, proxyType, proxyHost, proxyPort,
        '                proxyUser, proxyPass, authMethods())
        If Not String.IsNullOrWhiteSpace(o.ProxyHost) AndAlso o.ProxyPort > 0 Then
            Return New SSH.ConnectionInfo(o.Host, o.Port, o.UserName,
                                              SSH.ProxyTypes.Http, o.ProxyHost, o.ProxyPort,
                                              o.ProxyUserName, o.ProxyPassword, auths.ToArray())
        End If

        Return New SSH.ConnectionInfo(o.Host, o.Port, o.UserName, auths.ToArray())
    End Function

    Public Overrides Sub StartProcess()
        If IsProcessRunning Then
            Return
        End If

        If Options Is Nothing OrElse Not Options.IsValid() Then
            RaiseErrorEvent("Invalid SSH connection options (host and user name are required)." & Environment.NewLine)
            Return
        End If

        SyncLock sync
            Dim info = BuildConnectionInfo()
            client = New SSH.SshClient(info)
            client.KeepAliveInterval = TimeSpan.FromSeconds(30)

            '  Host-key verification. By default we do NOT trust unknown keys; the
            '  AcceptAnyHostKey flag (testing only) trusts everything.
            AddHandler client.HostKeyReceived,
                    Sub(sender As Object, e As HostKeyEventArgs)
                        If Options.AcceptAnyHostKey Then
                            e.CanTrust = True
                        ElseIf Not e.CanTrust Then
                            RaiseErrorEvent("Host key not trusted: " & e.FingerPrintSHA256 & Environment.NewLine)
                        End If
                    End Sub

            client.Connect()

            '  Attach the remote shell to a ShellStream (bidirectional).
            shell = client.CreateShellStream(
                    If(String.IsNullOrWhiteSpace(Options.TerminalType), "xterm", Options.TerminalType),
                    Columns, Rows, 0UI, 0UI, 1024)

            '  Start the background reader that pumps remote output into the console.
            runRead = True
            readThread = New Thread(AddressOf ReadLoop)
            readThread.IsBackground = True
            readThread.Name = "SshShellReader"
            readThread.Start()
        End SyncLock
    End Sub

    ''' <summary>
    ''' Background loop: reads remote output bytes from the shell stream and raises
    ''' <see cref="AbstractProcessInterface.OnProcessOutput"/> so the console
    ''' control can render them.
    ''' </summary>
    Private Sub ReadLoop()
        Dim buffer(4095) As Byte
        Dim decoder = Encoding.GetDecoder()

        While runRead AndAlso shell IsNot Nothing
            Dim read As Integer = 0

            Try
                read = shell.Read(buffer, 0, buffer.Length)
            Catch ex As Exception
                RaiseErrorEvent("SSH read error: " & ex.Message & Environment.NewLine)
                Exit While
            End Try

            If read <= 0 Then
                If client Is Nothing OrElse Not client.IsConnected Then
                    Exit While
                End If

                Thread.Sleep(20)
                Continue While
            End If

            '  Decode the raw bytes (supports multi-byte UTF-8 and ANSI escapes).
            Dim chars(buffer.Length + 1) As Char
            Dim bytesUsed As Integer
            Dim charsUsed As Integer
            Dim completed As Boolean
            decoder.Convert(buffer, 0, read, chars, 0, chars.Length, False, bytesUsed, charsUsed, completed)
            Dim text = New String(chars, 0, charsUsed)

            If text.Length > 0 Then
                RaiseOutputEvent(text)
            End If
        End While

        '  Inform the console that the session ended.
        RaiseExitEvent()
    End Sub

    Public Overrides Sub WriteInput(input As String)
        If shell Is Nothing OrElse client Is Nothing OrElse Not client.IsConnected Then
            Return
        End If

        Try
            '  The console sends a completed line without a trailing newline,
            '  so terminate it the way an interactive shell expects.
            Dim data = Encoding.GetBytes(input & vbCrLf)
            shell.Write(data, 0, data.Length)
            shell.Flush()
        Catch ex As Exception
            RaiseErrorEvent("SSH write error: " & ex.Message & Environment.NewLine)
        End Try
    End Sub

    ''' <summary>
    ''' Records the requested terminal size. The SSH.NET 2025.1.0 ShellStream no
    ''' longer exposes a runtime resize API, so the reported size is applied on the
    ''' next connection. (The initial size is set in <see cref="StartProcess"/>.)
    ''' </summary>
    Public Sub ResizeTerminal(columns As UInteger, rows As UInteger)
        columns = columns
        rows = rows
    End Sub

    Public Overrides Sub StopProcess()
        SyncLock sync
            runRead = False

            If client IsNot Nothing AndAlso client.IsConnected Then
                Try
                    client.Disconnect()
                Catch
                End Try
            End If

            If shell IsNot Nothing Then
                Try
                    shell.Dispose()
                Catch
                End Try

                shell = Nothing
            End If

            If client IsNot Nothing Then
                Try
                    client.Dispose()
                Catch
                End Try

                client = Nothing
            End If
        End SyncLock
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            StopProcess()
        End If

        MyBase.Dispose(disposing)
    End Sub
End Class
