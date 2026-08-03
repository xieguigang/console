Imports System.ComponentModel
Imports System.Text
Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.Windows.Forms.WebView2
Imports Microsoft.VisualBasic.Windows.Forms.Win32
Imports Microsoft.Web.WebView2.Core

''' <summary>
''' A terminal emulator control that renders through WebView2 instead of a
''' <see cref="RichTextBox"/>.
''' </summary>
''' <remarks>
''' This is an interface-compatible replacement for <see cref="ConsoleControl"/>:
''' both implement <see cref="IConsoleControl"/>, so callers can swap one for the
''' other.
''' <para>
''' All terminal semantics (ANSI parsing, the character grid, cursor handling and
''' painting) live in JavaScript inside the browser. This class is deliberately a
''' thin transport: it batches process output towards the renderer and forwards
''' keystrokes coming back from it to the <see cref="AbstractProcessInterface"/>.
''' Keeping the grid on the JavaScript side means a full-screen repaint costs one
''' small string over the message channel instead of serialising thousands of
''' cells per frame.
''' </para>
''' </remarks>
<DesignerCategory("Code")>
Partial Public Class WebViewConsole : Inherits UserControl
    Implements IConsoleControl

    ''' <summary>
    ''' Coalescing window for output pushed to the renderer.
    ''' </summary>
    ''' <remarks>
    ''' A noisy producer can raise <c>OnProcessOutput</c> thousands of times a
    ''' second. Merging everything that arrives within one frame bounds the number
    ''' of round trips to the display refresh rate, which is the whole point of
    ''' moving off the RichTextBox.
    ''' </remarks>
    Private Const FlushIntervalMs As Integer = 16

    ''' <summary>
    ''' The process back-end.
    ''' </summary>
    ''' <remarks>
    ''' Declared <c>WithEvents</c> so the <c>Handles</c> clauses below rebind
    ''' automatically whenever <see cref="SetConsoleCore"/> swaps the back-end.
    ''' </remarks>
    Protected WithEvents m_console As AbstractProcessInterface

    Private ReadOnly m_host As WebViewConsoleHost

    ''' <summary>
    ''' Output waiting to be handed to the renderer, either because it arrived
    ''' between flushes or because the browser has not signalled readiness yet.
    ''' </summary>
    Private ReadOnly m_pending As New StringBuilder()

    ''' <summary>
    ''' Guards <see cref="m_pending"/>, which is written from back-end worker
    ''' threads and drained on the UI thread.
    ''' </summary>
    Private ReadOnly m_pendingLock As New Object()

    Private WithEvents m_flushTimer As Timer

    Private m_rendererReady As Boolean
    Private m_isInputEnabled As Boolean = True
    Private m_readOnly As Boolean
    Private m_columns As Integer = 80
    Private m_rows As Integer = 24
    Private m_initialisationError As String

    ''' <summary>
    ''' Occurs when console output is produced.
    ''' </summary>
    Public Event OnConsoleOutput(sender As Object, args As ConsoleEventArgs) Implements IConsoleControl.OnConsoleOutput

    ''' <summary>
    ''' Occurs when console input is produced.
    ''' </summary>
    Public Event OnConsoleInput(sender As Object, args As ConsoleEventArgs) Implements IConsoleControl.OnConsoleInput

    ''' <summary>
    ''' Occurs when the back-end process exits.
    ''' </summary>
    Public Event ProcessExisted()

    ''' <summary>
    ''' Occurs after the renderer reports a new grid size, so hosts can resize the
    ''' pseudo terminal to match.
    ''' </summary>
    Public Event TerminalResized(columns As Integer, rows As Integer)

    Public Sub New()
        InitializeComponent()

        SetStyle(ControlStyles.ResizeRedraw, True)

        m_console = New ProcessInterface()
        m_host = New WebViewConsoleHost(WebView21)

        '  Registering with the designer's container means the timer is torn down
        '  by the generated Dispose override along with the rest of the control.
        If components Is Nothing Then
            components = New Container()
        End If

        m_flushTimer = New Timer(components) With {.Interval = FlushIntervalMs}

        InitialiseKeyMappings()
    End Sub

#Region "Interface-compatible properties"

    ''' <summary>
    ''' Gets or sets a value indicating whether the terminal rejects user input.
    ''' </summary>
    <Category("Console")>
    <DefaultValue(False)>
    Public Property [ReadOnly] As Boolean Implements IConsoleControl.ReadOnly
        Get
            Return m_readOnly
        End Get
        Set(value As Boolean)
            m_readOnly = value
            PushConfig()
        End Set
    End Property

    ''' <summary>
    ''' Gets or sets a value indicating whether typing is enabled.
    ''' </summary>
    <Category("Console")>
    <DefaultValue(True)>
    Public Property IsInputEnabled As Boolean Implements IConsoleControl.IsInputEnabled
        Get
            Return m_isInputEnabled
        End Get
        Set(value As Boolean)
            m_isInputEnabled = value
            PushConfig()
        End Set
    End Property

    ''' <summary>
    ''' Gets or sets a value indicating whether special keys (Tab, Ctrl-C, arrow
    ''' keys, ...) are forwarded to the process.
    ''' </summary>
    <Category("Console")>
    <DefaultValue(False)>
    Public Property SendKeyboardCommandsToProcess As Boolean Implements IConsoleControl.SendKeyboardCommandsToProcess
        Get
            Return m_sendKeyboardCommandsToProcess
        End Get
        Set(value As Boolean)
            m_sendKeyboardCommandsToProcess = value
            PushConfig()
        End Set
    End Property

    Private m_sendKeyboardCommandsToProcess As Boolean

    ''' <summary>
    ''' Gets or sets a value indicating whether diagnostic messages are shown.
    ''' </summary>
    <Category("Console")>
    <DefaultValue(False)>
    Public Property ShowDiagnostics As Boolean Implements IConsoleControl.ShowDiagnostics

    ''' <summary>
    ''' Gets a value indicating whether the back-end process is running.
    ''' </summary>
    <Browsable(False)>
    Public ReadOnly Property IsProcessRunning As Boolean Implements IConsoleControl.IsProcessRunning
        Get
            Return m_console IsNot Nothing AndAlso m_console.IsProcessRunning
        End Get
    End Property

    ''' <summary>
    ''' Gets the process interface currently driving this terminal.
    ''' </summary>
    <Browsable(False)>
    Public ReadOnly Property ProcessInterface As AbstractProcessInterface Implements IConsoleControl.ProcessInterface
        Get
            Return m_console
        End Get
    End Property

    ''' <summary>
    ''' Gets the key mappings forwarded to the renderer.
    ''' </summary>
    <Browsable(False)>
    Public ReadOnly Property KeyMappings As New List(Of KeyMapping) Implements IConsoleControl.KeyMappings

    ''' <summary>
    ''' Gets the column count last measured by the renderer.
    ''' </summary>
    ''' <remarks>
    ''' Unlike the RichTextBox control, this is not an estimate derived from font
    ''' metrics: the browser measures a real glyph and reports how many cells fit.
    ''' </remarks>
    <Browsable(False)>
    Public ReadOnly Property TerminalColumns As Integer Implements IConsoleControl.TerminalColumns
        Get
            Return m_columns
        End Get
    End Property

    ''' <summary>
    ''' Gets the row count last measured by the renderer.
    ''' </summary>
    <Browsable(False)>
    Public ReadOnly Property TerminalRows As Integer Implements IConsoleControl.TerminalRows
        Get
            Return m_rows
        End Get
    End Property

    ''' <summary>
    ''' Gets or sets how many scrolled-off lines the renderer retains.
    ''' </summary>
    <Category("Console")>
    <DefaultValue(5000)>
    Public Property ScrollbackLines As Integer = 5000

#End Region

#Region "Appearance"

    ''' <summary>
    ''' Gets or sets the terminal font. Changes are pushed to the renderer, which
    ''' re-measures the cell size and reports a new grid size.
    ''' </summary>
    Public Overrides Property Font As Font
        Get
            Return MyBase.Font
        End Get
        Set(value As Font)
            MyBase.Font = value
            PushStyle()
        End Set
    End Property

    ''' <summary>
    ''' Gets or sets the default foreground colour (SGR 39).
    ''' </summary>
    Public Overrides Property ForeColor As Color
        Get
            Return MyBase.ForeColor
        End Get
        Set(value As Color)
            MyBase.ForeColor = value
            PushStyle()
        End Set
    End Property

    ''' <summary>
    ''' Gets or sets the default background colour (SGR 49).
    ''' </summary>
    Public Overrides Property BackColor As Color
        Get
            Return MyBase.BackColor
        End Get
        Set(value As Color)
            MyBase.BackColor = value
            PushStyle()
        End Set
    End Property

#End Region

#Region "Lifecycle"

    Protected Overrides Sub OnHandleCreated(e As EventArgs)
        MyBase.OnHandleCreated(e)

        If DesignMode Then
            Return
        End If

        AddHandler m_host.Initialized, AddressOf OnHostInitialized
        AddHandler m_host.InitializationFailed, AddressOf OnHostInitializationFailed

        '  Fire and forget: the control stays usable while the browser starts,
        '  because everything written meanwhile is queued in m_pending.
        m_host.InitializeAsync(WebViewConsoleHost.DefaultUserDataFolder())
    End Sub

    Private Sub OnHostInitialized()
        '  Nothing to do yet: the renderer posts 'ready' once its scripts have
        '  run, and only then is it safe to send messages.
    End Sub

    Private Sub OnHostInitializationFailed(message As String)
        m_initialisationError = message

        '  Surface the failure in the control itself rather than throwing from an
        '  async void context, where nobody could catch it.
        If IsHandleCreated Then
            BeginInvoke(Sub() Invalidate())
        End If
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        MyBase.OnPaint(e)

        If String.IsNullOrEmpty(m_initialisationError) Then
            Return
        End If

        '  The WebView never came up, so paint the diagnostic ourselves.
        Using brush As New SolidBrush(Color.FromArgb(255, 107, 107))
            e.Graphics.FillRectangle(Brushes.Black, ClientRectangle)
            e.Graphics.DrawString(m_initialisationError, Font, brush,
                                  New RectangleF(8, 8, System.Math.Max(1, ClientSize.Width - 16), System.Math.Max(1, ClientSize.Height - 16)))
        End Using
    End Sub

    ''' <summary>
    ''' Initialises the key mappings.
    ''' </summary>
    ''' <remarks>
    ''' Mirrors <see cref="ConsoleControl.InitialiseKeyMappings"/>. Ctrl-C maps to
    ''' a bare ETX (0x03) so it reaches the remote program as an interrupt rather
    ''' than as a line of text.
    ''' </remarks>
    Private Sub InitialiseKeyMappings()
        KeyMappings.Add(New KeyMapping(False, False, False, Keys.Tab, "{TAB}", vbTab))
        KeyMappings.Add(New KeyMapping(True, False, False, Keys.C, "^(c)", ChrW(3)))
    End Sub

#End Region

#Region "Renderer messaging"

    ''' <summary>
    ''' Sends a message to the renderer, if it is listening.
    ''' </summary>
    Private Sub PostToRenderer(json As String)
        If Not m_rendererReady OrElse IsDisposed Then
            Return
        End If

        Dim core As CoreWebView2 = WebView21.CoreWebView2

        If core Is Nothing Then
            Return
        End If

        Try
            core.PostWebMessageAsString(json)
        Catch
            '  The browser can be torn down between the readiness check and the
            '  post; losing a frame of output is preferable to crashing the host.
        End Try
    End Sub

    Private Sub PushStyle()
        If Not m_rendererReady Then
            Return
        End If

        Dim face As Font = If(Font, New Font("Consolas", 9.75!))

        '  WinForms sizes fonts in points; CSS needs pixels.
        Dim pixels As Double = face.SizeInPoints * 96.0 / 72.0

        PostToRenderer(TerminalMessage.Style(face.Name, pixels, ForeColor, BackColor))
    End Sub

    Private Sub PushConfig()
        If Not m_rendererReady Then
            Return
        End If

        Dim payload = KeyMappings.Select(Function(m) New KeyMappingPayload With {
            .Ctrl = m.IsControlPressed,
            .Alt = m.IsAltPressed,
            .Shift = m.IsShiftPressed,
            .Key = ToDomKey(m.KeyCode),
            .Data = m.StreamMapping
        }).ToArray()

        PostToRenderer(TerminalMessage.Config(m_isInputEnabled, m_readOnly, m_sendKeyboardCommandsToProcess, payload))
    End Sub

    ''' <summary>
    ''' Translates a WinForms <see cref="Keys"/> value into the corresponding DOM
    ''' <c>KeyboardEvent.key</c> string used by the renderer.
    ''' </summary>
    Private Shared Function ToDomKey(key As Keys) As String
        Select Case key
            Case Keys.Tab : Return "Tab"
            Case Keys.Enter : Return "Enter"
            Case Keys.Escape : Return "Escape"
            Case Keys.Back : Return "Backspace"
            Case Keys.Delete : Return "Delete"
            Case Keys.Insert : Return "Insert"
            Case Keys.Home : Return "Home"
            Case Keys.End : Return "End"
            Case Keys.PageUp : Return "PageUp"
            Case Keys.PageDown : Return "PageDown"
            Case Keys.Up : Return "ArrowUp"
            Case Keys.Down : Return "ArrowDown"
            Case Keys.Left : Return "ArrowLeft"
            Case Keys.Right : Return "ArrowRight"
            Case Keys.Space : Return " "
        End Select

        If key >= Keys.F1 AndAlso key <= Keys.F12 Then
            Return "F" & (key - Keys.F1 + 1).ToString()
        End If

        If key >= Keys.A AndAlso key <= Keys.Z Then
            '  DOM reports the lower-case letter unless Shift is held, and the
            '  renderer compares case-sensitively.
            Return ChrW(AscW("a"c) + (key - Keys.A))
        End If

        If key >= Keys.D0 AndAlso key <= Keys.D9 Then
            Return ChrW(AscW("0"c) + (key - Keys.D0))
        End If

        Return key.ToString()
    End Function

    Private Sub OnWebMessageReceived(sender As Object, e As CoreWebView2WebMessageReceivedEventArgs) Handles WebView21.WebMessageReceived
        Dim json As String

        Try
            json = e.TryGetWebMessageAsString()
        Catch
            Return
        End Try

        Dim message As InboundMessage = TerminalMessage.Parse(json)

        If message Is Nothing Then
            Return
        End If

        Select Case message.Kind
            Case InboundMessageKind.Ready
                HandleRendererReady(message)

            Case InboundMessageKind.Input
                HandleInput(message.Data)

            Case InboundMessageKind.Raw
                HandleRaw(message.Data)

            Case InboundMessageKind.Resize
                HandleResize(message.Columns, message.Rows)

            Case InboundMessageKind.Bell
                Try
                    Media.SystemSounds.Beep.Play()
                Catch
                    '  Audio is optional.
                End Try
        End Select
    End Sub

    Private Sub HandleRendererReady(message As InboundMessage)
        m_rendererReady = True

        If message.Columns > 0 Then
            m_columns = message.Columns
        End If
        If message.Rows > 0 Then
            m_rows = message.Rows
        End If

        PushStyle()
        PushConfig()
        PostToRenderer(TerminalMessage.Scrollback(ScrollbackLines))

        '  Everything written while the browser was starting has been sitting in
        '  m_pending; release it now that there is something to render it.
        m_flushTimer.Start()
        Flush()

        RaiseEvent TerminalResized(m_columns, m_rows)
    End Sub

    Private Sub HandleInput(data As String)
        If m_console Is Nothing Then
            Return
        End If

        Try
            m_console.WriteInput(If(data, String.Empty))
        Catch ex As Exception
            WriteOutput(ex.Message & Environment.NewLine, Color.Red)
        End Try

        RaiseEvent OnConsoleInput(Me, New ConsoleEventArgs(data))
    End Sub

    Private Sub HandleRaw(data As String)
        If m_console Is Nothing OrElse String.IsNullOrEmpty(data) Then
            Return
        End If

        Try
            m_console.WriteRaw(data)
        Catch ex As Exception
            WriteOutput(ex.Message & Environment.NewLine, Color.Red)
        End Try
    End Sub

    Private Sub HandleResize(columns As Integer, rows As Integer)
        If columns <= 0 OrElse rows <= 0 Then
            Return
        End If

        If columns = m_columns AndAlso rows = m_rows Then
            Return
        End If

        m_columns = columns
        m_rows = rows

        RaiseEvent TerminalResized(columns, rows)
    End Sub

#End Region

#Region "Output batching"

    ''' <summary>
    ''' Queues raw terminal data for delivery to the renderer.
    ''' </summary>
    ''' <remarks>
    ''' Safe to call from any thread: the back-end raises its output events on
    ''' worker threads, and the timer drains the queue on the UI thread.
    ''' </remarks>
    Private Sub Enqueue(text As String)
        If String.IsNullOrEmpty(text) Then
            Return
        End If

        SyncLock m_pendingLock
            m_pending.Append(text)
        End SyncLock
    End Sub

    Private Sub OnFlushTick(sender As Object, e As EventArgs) Handles m_flushTimer.Tick
        Flush()
    End Sub

    ''' <summary>
    ''' Hands everything queued so far to the renderer in a single message.
    ''' </summary>
    Private Sub Flush()
        If Not m_rendererReady OrElse IsDisposed Then
            Return
        End If

        If InvokeRequired Then
            If IsHandleCreated Then
                BeginInvoke(New MethodInvoker(AddressOf Flush))
            End If
            Return
        End If

        Dim payload As String

        SyncLock m_pendingLock
            If m_pending.Length = 0 Then
                Return
            End If

            payload = m_pending.ToString()
            m_pending.Clear()
        End SyncLock

        PostToRenderer(TerminalMessage.Output(payload))
    End Sub

#End Region

#Region "IConsoleControl - writing"

    ''' <summary>
    ''' Writes text to the terminal in the given colour.
    ''' </summary>
    ''' <remarks>
    ''' The colour is applied by wrapping the text in a true-colour SGR pair, so
    ''' plain-text and ANSI output travel through exactly one rendering path.
    ''' </remarks>
    Public Overridable Sub WriteOutput(output As String, color As Color) Implements IConsoleControl.WriteOutput
        If String.IsNullOrEmpty(output) Then
            Return
        End If

        Dim escaped As String =
            ChrW(&H1B) & "[38;2;" & color.R & ";" & color.G & ";" & color.B & "m" &
            NormaliseNewLines(output) &
            ChrW(&H1B) & "[0m"

        Enqueue(escaped)
    End Sub

    ''' <summary>
    ''' Writes text that may contain ANSI escape sequences.
    ''' </summary>
    Public Overridable Sub WriteAnsiEscape(ansiText As String) Implements IConsoleControl.WriteAnsiEscape
        If String.IsNullOrEmpty(ansiText) Then
            Return
        End If

        Enqueue(NormaliseNewLines(ansiText))
    End Sub

    ''' <summary>
    ''' Sends a line of input to the process, optionally echoing it locally.
    ''' </summary>
    Public Overridable Sub WriteInput(input As String, color As Color, echo As Boolean) Implements IConsoleControl.WriteInput
        If echo Then
            WriteOutput(input, color)
        End If

        If m_console Is Nothing Then
            Return
        End If

        Try
            m_console.WriteInput(input)
        Catch ex As Exception
            WriteOutput(ex.Message & Environment.NewLine, Color.Red)
            Return
        End Try

        RaiseEvent OnConsoleInput(Me, New ConsoleEventArgs(input))
    End Sub

    ''' <summary>
    ''' Sends raw bytes to the process with no line terminator appended.
    ''' </summary>
    Public Overridable Sub WriteRaw(raw As String) Implements IConsoleControl.WriteRaw
        HandleRaw(raw)
    End Sub

    ''' <summary>
    ''' Clears the terminal, its scrollback and any queued output.
    ''' </summary>
    Public Overridable Sub ClearOutput() Implements IConsoleControl.ClearOutput
        SyncLock m_pendingLock
            m_pending.Clear()
        End SyncLock

        If InvokeRequired Then
            If IsHandleCreated Then
                BeginInvoke(New MethodInvoker(AddressOf ClearOutput))
            End If
            Return
        End If

        PostToRenderer(TerminalMessage.Clear())
    End Sub

    ''' <summary>
    ''' Normalises line endings to CRLF.
    ''' </summary>
    ''' <remarks>
    ''' A terminal grid treats CR and LF independently: a bare LF moves down but
    ''' keeps the column, producing staircased output for text that assumed a
    ''' teletype-style newline. Sources such as .NET exception messages emit bare
    ''' LFs, so they are promoted to CRLF here. Existing CRLF pairs are left
    ''' untouched.
    ''' </remarks>
    Private Shared Function NormaliseNewLines(text As String) As String
        If text.IndexOf(ChrW(10)) < 0 Then
            Return text
        End If

        Dim builder As New StringBuilder(text.Length + 16)

        For i As Integer = 0 To text.Length - 1
            Dim c As Char = text(i)

            If c = ChrW(10) AndAlso (i = 0 OrElse text(i - 1) <> ChrW(13)) Then
                builder.Append(ChrW(13))
            End If

            builder.Append(c)
        Next

        Return builder.ToString()
    End Function

#End Region

#Region "IConsoleControl - process control"

    ''' <summary>
    ''' Binds the terminal to a process back-end.
    ''' </summary>
    Public Sub SetConsoleCore([interface] As AbstractProcessInterface) Implements IConsoleControl.SetConsoleCore
        '  Assigning a WithEvents field rewires the Handles clauses automatically.
        m_console = [interface]
    End Sub

    Public Function GetInterface() As AbstractProcessInterface Implements IConsoleControl.GetInterface
        Return m_console
    End Function

    ''' <summary>
    ''' Starts the back-end session.
    ''' </summary>
    Public Overridable Sub StartProcess() Implements IConsoleControl.StartProcess
        If ShowDiagnostics Then
            WriteOutput("Starting session..." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
        End If

        m_console.StartProcess()
    End Sub

    ''' <summary>
    ''' Starts a local process.
    ''' </summary>
    Public Sub StartProcess(fileName As String, arguments As String) Implements IConsoleControl.StartProcess
        If ShowDiagnostics Then
            WriteOutput("Preparing to run " & fileName, Color.FromArgb(255, 0, 255, 0))

            If Not String.IsNullOrEmpty(arguments) Then
                WriteOutput(" with arguments " & arguments & "." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
            Else
                WriteOutput("." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
            End If
        End If

        If TypeOf m_console Is ProcessInterface Then
            Call DirectCast(m_console, ProcessInterface).StartProcess(fileName, arguments)
        Else
            Call "Can not start external process".warning
        End If
    End Sub

    ''' <summary>
    ''' Starts a local process from an explicit <see cref="ProcessStartInfo"/>.
    ''' </summary>
    Public Sub StartProcess(processStartInfo As ProcessStartInfo)
        If ShowDiagnostics Then
            WriteOutput("Preparing to run " & processStartInfo.FileName, Color.FromArgb(255, 0, 255, 0))

            If Not String.IsNullOrEmpty(processStartInfo.Arguments) Then
                WriteOutput(" with arguments " & processStartInfo.Arguments & "." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
            Else
                WriteOutput("." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
            End If
        End If

        If TypeOf m_console Is ProcessInterface Then
            Call DirectCast(m_console, ProcessInterface).StartProcess(processStartInfo)
        End If
    End Sub

    Public Sub StopProcess() Implements IConsoleControl.StopProcess
        Call m_console.StopProcess()
    End Sub

#End Region

#Region "Process back-end events"

    Private Sub processInterface_OnProcessOutput(sender As Object, args As ProcessEventArgs) Handles m_console.OnProcessOutput
        '  The back-end may or may not declare ANSI content. Sniffing for ESC as
        '  well keeps back-ends that emit escapes without setting the flag working,
        '  matching ConsoleControl's behaviour.
        If args.Ansi OrElse (args.Content IsNot Nothing AndAlso args.Content.IndexOf(ChrW(&H1B)) >= 0) Then
            WriteAnsiEscape(args.Content)
        Else
            WriteOutput(args.Content, Color.White)
        End If

        RaiseEvent OnConsoleOutput(Me, New ConsoleEventArgs(args.Content))
    End Sub

    Private Sub processInterface_OnProcessError(sender As Object, args As ProcessEventArgs) Handles m_console.OnProcessError
        WriteOutput(args.Content, Color.Red)

        RaiseEvent OnConsoleOutput(Me, New ConsoleEventArgs(args.Content))
    End Sub

    Private Sub processInterface_OnProcessInput(sender As Object, args As ProcessEventArgs) Handles m_console.OnProcessInput

    End Sub

    Private Sub processInterface_OnProcessExit(sender As Object, args As ProcessEventArgs) Handles m_console.OnProcessExit
        If ShowDiagnostics AndAlso TypeOf m_console Is ProcessInterface Then
            WriteOutput(Environment.NewLine & DirectCast(m_console, ProcessInterface).ProcessFileName & " exited.",
                        Color.FromArgb(255, 0, 255, 0))
        End If

        RaiseEvent ProcessExisted()
    End Sub

#End Region

#Region "Focus"

    Protected Overrides Sub OnGotFocus(e As EventArgs)
        MyBase.OnGotFocus(e)

        '  Focus lands on the UserControl, but the keyboard sink lives inside the
        '  document, so it has to be handed on explicitly.
        PostToRenderer(TerminalMessage.Focus())
    End Sub

    ''' <summary>
    ''' Moves keyboard focus into the terminal.
    ''' </summary>
    Public Sub FocusTerminal()
        If WebView21 IsNot Nothing AndAlso Not WebView21.IsDisposed Then
            WebView21.Focus()
        End If

        PostToRenderer(TerminalMessage.Focus())
    End Sub

#End Region

End Class
