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
        Me.ansi = True
    End Sub

    Public Overrides ReadOnly Property IsProcessRunning As Boolean
        Get
            Return client IsNot Nothing AndAlso client.IsConnected
        End Get
    End Property

    ''' <summary>
    ''' The remote end runs a real PTY, so it owns echo, line editing and control
    ''' characters; every keystroke has to reach it unaltered for interactive
    ''' programs and Ctrl+C to work.
    ''' </summary>
    Public Overrides ReadOnly Property PreferredInputMode As ConsoleInputMode
        Get
            Return ConsoleInputMode.Raw
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
                If CheckExit(decoder, buffer) Then
                    Exit While
                End If
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

    Private Function CheckExit(decoder As Decoder, buffer As Byte()) As Boolean
        '  A blocking read returning 0 (or negative) is EOF: the remote shell
        '  channel has been closed. The SSH transport connection may still be
        '  "Connected", so we must NOT gate the exit on client.IsConnected —
        '  doing so spins forever. Flush any buffered decoder bytes, then exit.
        Dim chars(buffer.Length + 1) As Char
        Dim bytesUsed As Integer
        Dim charsUsed As Integer
        Dim completed As Boolean

        decoder.Convert(buffer, 0, 0, chars, 0, chars.Length, True, bytesUsed, charsUsed, completed)

        If charsUsed > 0 Then
            RaiseOutputEvent(New String(chars, 0, charsUsed))
        End If

        Return True
    End Function

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
    ''' Writes raw input to the remote shell without appending any line terminator.
    ''' This is required to deliver control signals such as Ctrl+C (<c>ChrW(3)</c>)
    ''' intact, so the remote process group can be interrupted.
    ''' </summary>
    ''' <param name="input">The raw input to send.</param>
    Public Overrides Sub WriteRaw(input As String)
        If shell Is Nothing OrElse client Is Nothing OrElse Not client.IsConnected Then
            Return
        End If

        Try
            Dim data = Encoding.GetBytes(input)
            shell.Write(data, 0, data.Length)
            shell.Flush()
        Catch ex As Exception
            RaiseErrorEvent("SSH write error: " & ex.Message & Environment.NewLine)
        End Try
    End Sub

    ''' <summary>
    ''' Applies a new terminal size to the remote session.
    ''' <para>
    ''' The size is first stored in <see cref="Columns"/>/<see cref="Rows"/> so a
    ''' later reconnect recreates the shell at the current size, then a
    ''' <c>window-change</c> request is sent over the live channel via
    ''' <see cref="SSH.ShellStream.ChangeWindowSize"/>. That request makes the
    ''' remote PTY deliver SIGWINCH to its foreground process group, which is what
    ''' full-screen programs (htop, btop, vim, ...) need in order to re-layout.
    ''' Without it the remote side keeps rendering at the size negotiated when the
    ''' channel was opened, so the output no longer lines up with the local grid.
    ''' </para>
    ''' <para>
    ''' The pixel dimensions are passed as 0, matching how the shell stream is
    ''' created in <see cref="StartProcess"/>: the character cell grid is
    ''' authoritative and the server derives the pixel size from it.
    ''' </para>
    ''' </summary>
    ''' <param name="columns">The new terminal width in character cells.</param>
    ''' <param name="rows">The new terminal height in character cells.</param>
    Public Sub ResizeTerminal(columns As UInteger, rows As UInteger)
        If columns <= 0UI OrElse rows <= 0UI Then
            Return
        End If

        '  Remember the size even when no session is live, so a reconnect starts
        '  out at the size the console currently shows.
        Me.Columns = columns
        Me.Rows = rows

        '  Take a local reference under the lock: the background reader may run
        '  StopProcess() on EOF and null out shell between a check and its use.
        Dim target As SSH.ShellStream = Nothing

        SyncLock sync
            If shell Is Nothing OrElse client Is Nothing OrElse Not client.IsConnected Then
                Return
            End If

            target = shell
        End SyncLock

        Try
            target.ChangeWindowSize(columns, rows, 0UI, 0UI)
        Catch ex As ObjectDisposedException
            '  The session was torn down while resizing; nothing to report.
        Catch ex As Exception
            '  A failed resize is not fatal, and this runs on the UI thread while
            '  the user drags the window border, so the exception must not escape.
            RaiseErrorEvent("SSH resize error: " & ex.Message & Environment.NewLine)
        End Try
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
