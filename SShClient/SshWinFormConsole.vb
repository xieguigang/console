Imports System.ComponentModel
Imports Microsoft.VisualBasic.Windows.Forms
Imports Microsoft.VisualBasic.Windows.Forms.Win32
Imports std = System.Math

''' <summary>
''' A ready-to-use WinForms console control that opens an interactive SSH shell.
''' It derives from <c>ConsoleControl</c> and plugs an <see cref="SshProcessInterface"/>
''' in as the back-end, so all ANSI rendering, line editing and input handling is
''' inherited unchanged.
''' </summary>
Public Class SshWinFormConsole : Inherits UserControl

    Private sshInterface As SshProcessInterface = Nothing
    '  Use TerminalControl (grid-based renderer) so htop/btop clear-screen
    '  repaints and Ctrl+C interrupt signalling work correctly.
    Friend WithEvents ConsoleControl1 As ConsoleControl
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
        EstimateAndApplyTerminalSize(sshInterface)

        '  The base ConsoleControl already renders OnProcessOutput; we only need
        '  to surface errors / session end in addition.
        AddHandler sshInterface.OnProcessError, AddressOf OnSshError
        AddHandler sshInterface.OnProcessExit, AddressOf OnSshExit

        '  Assign the back-end and start the session through the inherited API.
        ConsoleControl1.SetConsoleCore(sshInterface)
        ConsoleControl1.StartProcess()
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
    ''' Estimates terminal rows/columns from the control size and font, then
    ''' applies them to the SSH back-end.
    ''' </summary>
    Private Sub EstimateAndApplyTerminalSize(backend As SshProcessInterface)
        Dim font = GetConsoleFont()
        If font Is Nothing Then
            backend.Columns = 80UI
            backend.Rows = 24UI
            Return
        End If

        Using g = Me.CreateGraphics()
            Dim charSize = g.MeasureString("M", font)
            If charSize.Width > 0 AndAlso charSize.Height > 0 Then
                backend.Columns = CUInt(std.Max(1, CInt(Me.ClientSize.Width \ charSize.Width)))
                backend.Rows = CUInt(std.Max(1, CInt(Me.ClientSize.Height \ charSize.Height)))
            End If
        End Using
    End Sub

    ''' <summary>Resolves the monospace font used by the embedded console.</summary>
    Private Function GetConsoleFont() As Font
        '  The base ConsoleControl initializes its RichTextBox with Consolas 9.75pt.
        '  Reach it through reflection so we do not depend on its (private) field.
        Try
            Dim rtb = GetType(ConsoleControl) _
                    .GetField("richTextBoxConsole", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance) _
                    ?.GetValue(Me)

            If rtb IsNot Nothing Then
                Return DirectCast(rtb, RichTextBox).Font
            End If
        Catch
        End Try

        Return New Font("Consolas", 9.75F, FontStyle.Regular, GraphicsUnit.Point)
    End Function

    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)

        If m_autoConnectOnFocus AndAlso Not ConsoleControl1.IsProcessRunning AndAlso ConnectionOptions.IsValid() Then
            Connect()
        End If
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)

        If sshInterface IsNot Nothing AndAlso ConsoleControl1.IsProcessRunning Then
            EstimateAndApplyTerminalSize(sshInterface)
            sshInterface.ResizeTerminal(sshInterface.Columns, sshInterface.Rows)
        End If
    End Sub

    Private Sub OnSshError(sender As Object, e As ProcessEventArgs)
        ConsoleControl1.WriteOutput(e.Content, Color.Red)
    End Sub

    Sub New()
        Call InitializeComponent()
    End Sub

    Private Sub InitializeComponent()
        ConsoleControl1 = New ConsoleControl()
        SuspendLayout()
        ' 
        ' ConsoleControl1
        ' 
        ConsoleControl1.Dock = DockStyle.Fill
        ConsoleControl1.IsInputEnabled = True
        ConsoleControl1.Location = New Point(0, 0)
        ConsoleControl1.Margin = New Padding(4, 4, 4, 4)
        ConsoleControl1.Name = "ConsoleControl1"
        ConsoleControl1.ReadOnly = True
        '  Keyboard commands (incl. Ctrl+C) are forwarded to the shell so control
        '  signals reach the remote process and can interrupt programs such as htop.
        ConsoleControl1.SendKeyboardCommandsToProcess = True
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
        ConsoleControl1.WriteOutput(Environment.NewLine & "SSH session closed." & Environment.NewLine, Color.Gray)
    End Sub
End Class
