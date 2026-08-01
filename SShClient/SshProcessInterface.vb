Imports System.IO
Imports System.Text
Imports System.Threading
Imports Renci.SshNet.Common
Imports SSH = Renci.SshNet
Imports Microsoft.VisualBasic.Windows.Forms.Win32

Namespace SShClient

    ''' <summary>
    ''' An <see cref="AbstractProcessInterface"/> implementation that drives a remote
    ''' shell over SSH using the SSH.NET library. The remote shell is attached to a
    ''' bidirectional <see cref="PipeStream"/> via <see cref="SSH.SshClient.CreateShell"/>,
    ''' so the hosting console control reuses its existing ANSI rendering and input
    ''' pipeline unchanged.
    ''' </summary>
    Public Class SshProcessInterface : Inherits AbstractProcessInterface

        Private client As SSH.SshClient = Nothing
        Private pipe As PipeStream = Nothing
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
        ''' optional HTTP/SOCKS proxy, mirroring the reference implementation.
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

            Dim info As SSH.ConnectionInfo

            If Not String.IsNullOrWhiteSpace(o.ProxyHost) AndAlso o.ProxyPort > 0 Then
                Dim proxyType = SSH.ProxyTypes.Http
                Dim proxy = New SSH.ProxyInfo(proxyType, o.ProxyHost, o.ProxyPort, o.ProxyUserName, o.ProxyPassword)
                info = New SSH.ConnectionInfo(o.Host, o.Port, o.UserName, proxy, auths.ToArray())
            Else
                info = New SSH.ConnectionInfo(o.Host, o.Port, o.UserName, auths.ToArray())
            End If

            If o.AcceptAnyHostKey Then
                '  Insecure: trust any host key (testing only).
                AddHandler info.HostKeyReceived,
                    Sub(sender As Object, e As SSH.HostKeyEventArgs)
                        e.CanTrust = True
                    End Sub
            End If

            Return info
        End Function

        Public Overrides Sub StartProcess()
            If IsProcessRunning Then
                Return
            End If

            If Options Is Nothing OrElse Not Options.IsValid() Then
                RaiseEvent OnProcessError(Me, New ProcessEventArgs("Invalid SSH connection options (host and user name are required)." & Environment.NewLine))
                Return
            End If

            SyncLock sync
                Dim info = BuildConnectionInfo()
                client = New SSH.SshClient(info)
                client.KeepAliveInterval = TimeSpan.FromSeconds(30)

                If Not Options.AcceptAnyHostKey Then
                    '  Report untrusted host keys as errors instead of silently trusting them.
                    AddHandler client.HostKeyReceived,
                        Sub(sender As Object, e As SSH.HostKeyEventArgs)
                            If Not e.CanTrust Then
                                RaiseEvent OnProcessError(Me, New ProcessEventArgs("Host key received but not trusted: " & e.FingerPrint & Environment.NewLine))
                            End If
                        End Sub
                End If

                client.Connect()

                '  Attach the remote shell to a bidirectional pipe.
                pipe = New PipeStream()
                client.CreateShell(pipe, Rows, Columns, 0UI, 0UI, If(String.IsNullOrWhiteSpace(Options.TerminalType), "xterm", Options.TerminalType))

                '  Start the background reader that pumps remote output into the console.
                runRead = True
                readThread = New Thread(AddressOf ReadLoop)
                readThread.IsBackground = True
                readThread.Name = "SshShellReader"
                readThread.Start()
            End SyncLock
        End Sub

        ''' <summary>
        ''' Background loop: reads remote output bytes from the pipe and raises
        ''' <see cref="AbstractProcessInterface.OnProcessOutput"/> so the console
        ''' control can render them.
        ''' </summary>
        Private Sub ReadLoop()
            Dim buffer(4095) As Byte
            Dim decoder = Encoding.GetDecoder()

            While runRead AndAlso pipe IsNot Nothing
                Dim read As Integer = 0

                Try
                    read = pipe.Read(buffer, 0, buffer.Length)
                Catch ex As Exception
                    RaiseEvent OnProcessError(Me, New ProcessEventArgs("SSH read error: " & ex.Message & Environment.NewLine))
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
                Dim used As Integer
                Dim completed As Boolean
                decoder.Convert(buffer, 0, read, chars, 0, chars.Length, False, used, completed)
                Dim text = New String(chars, 0, used)

                If text.Length > 0 Then
                    RaiseEvent OnProcessOutput(Me, New ProcessEventArgs(text))
                End If
            End While

            '  Inform the console that the session ended.
            RaiseEvent OnProcessExit(Me, New ProcessEventArgs(String.Empty))
        End Sub

        Public Overrides Sub WriteInput(input As String)
            If pipe Is Nothing OrElse client Is Nothing OrElse Not client.IsConnected Then
                Return
            End If

            Try
                '  The console sends a completed line without a trailing newline,
                '  so terminate it the way an interactive shell expects.
                Dim data = Encoding.GetBytes(input & vbCrLf)
                pipe.Write(data, 0, data.Length)
                pipe.Flush()
            Catch ex As Exception
                RaiseEvent OnProcessError(Me, New ProcessEventArgs("SSH write error: " & ex.Message & Environment.NewLine))
            End Try
        End Sub

        ''' <summary>
        ''' Updates the remote terminal size. Called by the hosting control when the
        ''' on-screen console is resized.
        ''' </summary>
        Public Sub ResizeTerminal(columns As UInteger, rows As UInteger)
            Columns = columns
            Rows = rows

            If client IsNot Nothing AndAlso client.IsConnected Then
                SyncLock sync
                    Try
                        client.SendShellResizeRequest(rows, columns, 0UI, 0UI)
                    Catch
                        '  Resize is best-effort; ignore failures.
                    End Try
                End SyncLock
            End If
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

                If client IsNot Nothing Then
                    Try
                        client.Dispose()
                    Catch
                    End Try

                    client = Nothing
                End If

                pipe = Nothing
            End SyncLock
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                StopProcess()
            End If

            MyBase.Dispose(disposing)
        End Sub
    End Class
End Namespace
