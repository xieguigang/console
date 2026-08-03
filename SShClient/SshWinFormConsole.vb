Imports System.ComponentModel
Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.Windows.Forms
Imports Microsoft.VisualBasic.Windows.Forms.Win32
Imports std = System.Math

''' <summary>
''' A ready-to-use WinForms console control that opens an interactive SSH shell.
''' It hosts a <see cref="WebViewConsole"/> and plugs an <see cref="SshProcessInterface"/>
''' in as the back-end, so all ANSI rendering, line editing and input handling is
''' delegated to the terminal control unchanged.
''' </summary>
Public Class SshWinFormConsole : Inherits UserControl

    Private sshInterface As SshProcessInterface = Nothing
    Private WithEvents localInterface As LocalShellInterface = Nothing
    '  WebView2-backed renderer: the character grid, ANSI parsing and painting all
    '  run in the browser, so full-screen repaints (htop/btop) and Ctrl+C
    '  interrupt signalling behave like a real terminal.
    Friend WithEvents ConsoleControl1 As WebViewConsole
    Private m_autoConnectOnFocus As Boolean = False

    ''' <summary>
    ''' Gets or sets the connection options. Assign the host/user/password (or a
    ''' private key) here before calling <see cref="Connect"/>.
    ''' </summary>
    <Browsable(True), Category("SSH")>
    Public Property ConnectionOptions As New SshConnectionOptions()

    ''' <summary>
    ''' When true, the control automatically connects the first time it receives
    ''' focus (provided the connection options are valid).
    ''' </summary>
    <Browsable(True), Category("SSH"), DefaultValue(False)>
    Public Property AutoConnectOnFocus As Boolean
        Get
            Return m_autoConnectOnFocus
        End Get
        Set(value As Boolean)
            m_autoConnectOnFocus = value
        End Set
    End Property

    ''' <summary>Convenience shortcut for <see cref="ConnectionOptions.Host"/>.</summary>
    <Browsable(True), Category("SSH")>
    Public Property Host As String
        Get
            Return ConnectionOptions.Host
        End Get
        Set(value As String)
            ConnectionOptions.Host = value
        End Set
    End Property

    ''' <summary>Convenience shortcut for <see cref="ConnectionOptions.Port"/>.</summary>
    <Browsable(True), Category("SSH"), DefaultValue(22)>
    Public Property Port As Integer
        Get
            Return ConnectionOptions.Port
        End Get
        Set(value As Integer)
            ConnectionOptions.Port = value
        End Set
    End Property

    ''' <summary>Convenience shortcut for <see cref="ConnectionOptions.UserName"/>.</summary>
    <Browsable(True), Category("SSH")>
    Public Property UserName As String
        Get
            Return ConnectionOptions.UserName
        End Get
        Set(value As String)
            ConnectionOptions.UserName = value
        End Set
    End Property

    ''' <summary>Convenience shortcut for <see cref="ConnectionOptions.Password"/>.</summary>
    <Browsable(True), Category("SSH")>
    Public Property Password As String
        Get
            Return ConnectionOptions.Password
        End Get
        Set(value As String)
            ConnectionOptions.Password = value
        End Set
    End Property

    Public Property IsInputEnabled As Boolean
        Get
            Return ConsoleControl1.IsInputEnabled
        End Get
        Set(value As Boolean)
            ConsoleControl1.IsInputEnabled = value
        End Set
    End Property

    Public Property [ReadOnly] As Boolean
        Get
            Return ConsoleControl1.ReadOnly
        End Get
        Set(value As Boolean)
            ConsoleControl1.ReadOnly = value
        End Set
    End Property

    Public Property SendKeyboardCommandsToProcess As Boolean
        Get
            Return ConsoleControl1.SendKeyboardCommandsToProcess
        End Get
        Set(value As Boolean)
            ConsoleControl1.SendKeyboardCommandsToProcess = value
        End Set
    End Property

    Public Property ShowDiagnostics As Boolean
        Get
            Return ConsoleControl1.ShowDiagnostics
        End Get
        Set(value As Boolean)
            ConsoleControl1.ShowDiagnostics = value
        End Set
    End Property

    ''' <summary>Connects using the currently configured <see cref="ConnectionOptions"/>.</summary>
    Public Sub Connect()
        Connect(ConnectionOptions)
    End Sub

    ''' <summary>Connects using the supplied options, then starts the session.</summary>
    Public Sub Connect(options As SshConnectionOptions)
        If options Is Nothing OrElse Not options.IsValid() Then
            ConsoleControl1.WriteOutput("SSH connection options are incomplete (host and user name required)." & Environment.NewLine, Color.Red)
            Return
        End If

        If ConsoleControl1.IsProcessRunning Then
            Disconnect()
        End If

        '  Build the back-end, estimate the terminal size and wire it up.
        sshInterface = New SshProcessInterface(options)
        ApplyTerminalSize(sshInterface)

        '  The terminal control already renders OnProcessOutput; we only need to
        '  surface errors / session end in addition.
        AddHandler sshInterface.OnProcessError, AddressOf OnSshError
        AddHandler sshInterface.OnProcessExit, AddressOf OnSshExit

        '  Assign the back-end and start the session through the inherited API.
        ConsoleControl1.SetConsoleCore(sshInterface)
        ConsoleControl1.StartProcess()
        ConsoleControl1.FocusTerminal()
    End Sub

    ''' <summary>Disconnects the active SSH session.</summary>
    Public Sub Disconnect()
        Call ConsoleControl1.StopProcess()

        If sshInterface IsNot Nothing Then
            RemoveHandler sshInterface.OnProcessError, AddressOf OnSshError
            RemoveHandler sshInterface.OnProcessExit, AddressOf OnSshExit
            sshInterface.Dispose()
            sshInterface = Nothing
        End If
    End Sub

    ''' <summary>
    ''' Applies the terminal's current grid size to the SSH back-end.
    ''' </summary>
    ''' <remarks>
    ''' The size is taken from the renderer, which measures a real glyph in the
    ''' browser, rather than estimated from font metrics on this side. That keeps
    ''' the pty window exactly in step with what is actually displayed, which
    ''' full-screen programs rely on to lay themselves out.
    ''' </remarks>
    Private Sub ApplyTerminalSize(backend As SshProcessInterface)
        backend.Columns = CUInt(std.Max(1, ConsoleControl1.TerminalColumns))
        backend.Rows = CUInt(std.Max(1, ConsoleControl1.TerminalRows))
    End Sub

    ''' <summary>
    ''' Propagates a renderer-reported grid change to the live SSH session.
    ''' </summary>
    ''' <remarks>
    ''' Driven by the terminal rather than by <c>OnResize</c>: the browser needs a
    ''' layout pass before it can report the new size, so resizing off the WinForms
    ''' event would push a stale row/column count to the remote host.
    ''' </remarks>
    Private Sub OnTerminalResized(columns As Integer, rows As Integer) Handles ConsoleControl1.TerminalResized
        If sshInterface Is Nothing OrElse Not ConsoleControl1.IsProcessRunning Then
            Return
        End If

        ApplyTerminalSize(sshInterface)
        sshInterface.ResizeTerminal(sshInterface.Columns, sshInterface.Rows)
    End Sub

    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)

        If m_autoConnectOnFocus AndAlso Not ConsoleControl1.IsProcessRunning AndAlso ConnectionOptions.IsValid() Then
            Connect()
        End If
    End Sub

    Private Sub OnSshError(sender As Object, e As ProcessEventArgs)
        ConsoleControl1.WriteOutput(e.Content, Color.Red)
    End Sub

    Sub New()
        Call InitializeComponent()
    End Sub

    Private Sub InitializeComponent()
        ConsoleControl1 = New WebViewConsole()
        SuspendLayout()
        ' 
        ' ConsoleControl1
        ' 
        ConsoleControl1.Dock = DockStyle.Fill
        ConsoleControl1.IsInputEnabled = True
        ConsoleControl1.Location = New Point(0, 0)
        ConsoleControl1.Margin = New Padding(4, 4, 4, 4)
        ConsoleControl1.Name = "ConsoleControl1"

        '  Key forwarding is deliberately left alone: the console derives it from
        '  whichever back-end is bound, so the local shell gets line editing and
        '  the SSH session gets raw pass-through (which is what lets Ctrl+C
        '  interrupt programs such as htop). Setting it here would pin the control
        '  to one mode and break the other.
        ConsoleControl1.ShowDiagnostics = False
        ConsoleControl1.Size = New Size(852, 663)
        ConsoleControl1.TabIndex = 0
        ' 
        ' SshWinFormConsole
        ' 
        Controls.Add(ConsoleControl1)
        Name = "SshWinFormConsole"
        Size = New Size(852, 663)
        ResumeLayout(False)

    End Sub

    Private Sub OnSshExit(sender As Object, e As ProcessEventArgs)
        '  OnSshExit is raised on the background reader thread; marshal to UI.
        If ConsoleControl1.InvokeRequired Then
            ConsoleControl1.Invoke(New MethodInvoker(Sub() OnSshExit(sender, e)))
            Return
        End If

        ConsoleControl1.WriteOutput(Environment.NewLine & "SSH session closed." & Environment.NewLine, Color.Gray)
        Disconnect()
        StartLocalShell()
    End Sub

    ''' <summary>
    ''' Creates a fresh <see cref="LocalShellInterface"/>, subscribes to its
    ''' <see cref="LocalShellInterface.SshConnectRequested"/> event, assigns it
    ''' as the console back-end and starts the local session.
    ''' </summary>
    Private Sub StartLocalShell()
        '  WithEvents + Handles auto-wires SshConnectRequested when the field is set.
        localInterface = New LocalShellInterface()
        ConsoleControl1.SetConsoleCore(localInterface)
        ConsoleControl1.StartProcess()
        ConsoleControl1.FocusTerminal()
    End Sub

    ''' <summary>
    ''' Handles the <c>ssh [-p port] user@host</c> command from the local shell
    ''' by creating a real SSH connection.
    ''' </summary>
    Private Sub OnSshConnectRequested(options As SshConnectionOptions) Handles localInterface.SshConnectRequested
        Connect(options)
    End Sub

    Private Sub SshWinFormConsole_Load(sender As Object, e As EventArgs) Handles Me.Load
        StartLocalShell()
    End Sub
End Class
