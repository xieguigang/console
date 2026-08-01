Imports System.ComponentModel
Imports Microsoft.VisualBasic.Windows.Forms.Win32

''' <summary>
''' The Console Control allows you to embed a basic console in your application.
''' </summary>
Partial Public Class ConsoleControl : Inherits UserControl

    Public Property [ReadOnly] As Boolean
        Get
            Return richTextBoxConsole.ReadOnly
        End Get
        Set(value As Boolean)
            richTextBoxConsole.ReadOnly = value
        End Set
    End Property

    ''' <summary>
    ''' The internal process interface used to interface with the process.
    ''' </summary>
    Protected WithEvents m_console As AbstractProcessInterface

    ''' <summary>
    ''' Current position that input starts at.
    ''' </summary>
    Private inputStart As Integer = -1

    ''' <summary>
    ''' The is input enabled flag.
    ''' </summary>
    Private m_isInputEnabled As Boolean = True

    ''' <summary>
    ''' The last input string (used so that we can make sure we don't echo input twice).
    ''' </summary>
    Private lastInput As String

    ''' <summary>
    ''' The list of previously submitted input strings, used for up/down history navigation.
    ''' </summary>
    Private inputHistory As New List(Of String)

    ''' <summary>
    ''' The current index into <see cref="inputHistory"/>. Equals <c>inputHistory.Count</c>
    ''' to indicate the (empty or in-progress) current input line.
    ''' </summary>
    Private historyIndex As Integer = 0

    ''' <summary>
    ''' Occurs when console output is produced.
    ''' </summary>
    Public Event OnConsoleOutput(sender As Object, args As ConsoleEventArgs)

    ''' <summary>
    ''' Occurs when console input is produced.
    ''' </summary>
    Public Event OnConsoleInput(sender As Object, args As ConsoleEventArgs)

    ''' <summary>
    ''' Gets or sets a value indicating whether to show diagnostics.
    ''' </summary>
    ''' <value>
    '''   <c>true</c> if show diagnostics; otherwise, <c>false</c>.
    ''' </value>
    <Category("Console Control"), Description("Show diagnostic information, such as exceptions.")>
    Public Property ShowDiagnostics As Boolean

    ''' <summary>
    ''' Gets or sets a value indicating whether this instance is input enabled.
    ''' </summary>
    ''' <value>
    ''' 	<c>true</c> if this instance is input enabled; otherwise, <c>false</c>.
    ''' </value>
    <Category("Console Control"), Description("If true, the user can key in input.")>
    Public Property IsInputEnabled As Boolean
        Get
            Return m_isInputEnabled
        End Get
        Set(value As Boolean)
            m_isInputEnabled = value

            If IsProcessRunning Then
                richTextBoxConsole.ReadOnly = Not value
            End If
        End Set
    End Property

    ''' <summary>
    ''' Gets or sets a value indicating whether [send keyboard commands to process].
    ''' </summary>
    ''' <value>
    ''' 	<c>true</c> if [send keyboard commands to process]; otherwise, <c>false</c>.
    ''' </value>
    <Category("Console Control"), Description("If true, special keyboard commands like Ctrl-C and tab are sent to the process.")>
    Public Property SendKeyboardCommandsToProcess As Boolean

    ''' <summary>
    ''' Gets a value indicating whether this instance is process running.
    ''' </summary>
    ''' <value>
    ''' 	<c>true</c> if this instance is process running; otherwise, <c>false</c>.
    ''' </value>
    <Browsable(False)>
    Public ReadOnly Property IsProcessRunning As Boolean
        Get
            '  Delegate to the (possibly overridden) back-end property so that any
            '  AbstractProcessInterface implementation (local process, SSH, ...) works.
            Return m_console.IsProcessRunning
        End Get
    End Property

    ''' <summary>
    ''' Gets the internal rich text box.
    ''' </summary>
    <Browsable(False)>
    Public ReadOnly Property InternalRichTextBox As RichTextBox
        Get
            Return richTextBoxConsole
        End Get
    End Property

    ''' <summary>
    ''' Gets the process interface.
    ''' </summary>
    <Browsable(False)>
    Public ReadOnly Property ProcessInterface As AbstractProcessInterface
        Get
            Return m_console
        End Get
    End Property

    ''' <summary>
    ''' Gets the key mappings.
    ''' </summary>
    <Browsable(False)>
    Public ReadOnly Property KeyMappings As New List(Of KeyMapping)

    ''' <summary>
    ''' Gets or sets the font of the text displayed by the control.
    ''' </summary>
    ''' <returns>The <seecref="T:System.Drawing.Font"/> to apply to the text displayed by the control. The default is the value of the <seecref="P:System.Windows.Forms.Control.DefaultFont"/> property.</returns>
    '''   <PermissionSet>
    '''   <IPermissionclass="System.Security.Permissions.EnvironmentPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"version="1"Unrestricted="true"/>
    '''   <IPermissionclass="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"version="1"Unrestricted="true"/>
    '''   <IPermissionclass="System.Security.Permissions.SecurityPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"version="1"Flags="UnmanagedCode, ControlEvidence"/>
    '''   <IPermissionclass="System.Diagnostics.PerformanceCounterPermission, System, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"version="1"Unrestricted="true"/>
    '''   </PermissionSet>
    Public Overrides Property Font As Font
        Get
            '  Return the base class font.
            Return MyBase.Font
        End Get
        Set(value As Font)
            '  Set the base class font...
            MyBase.Font = value

            '  ...and the internal control font.
            richTextBoxConsole.Font = value
        End Set
    End Property

    ''' <summary>
    ''' Gets or sets the background color for the control.
    ''' </summary>
    ''' <returns>A <seecref="T:System.Drawing.Color"/> that represents the background color of the control. The default is the value of the <seecref="P:System.Windows.Forms.Control.DefaultBackColor"/> property.</returns>
    '''   <PermissionSet>
    '''   <IPermissionclass="System.Security.Permissions.FileIOPermission, mscorlib, Version=2.0.3600.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"version="1"Unrestricted="true"/>
    '''   </PermissionSet>
    Public Overrides Property BackColor As Color
        Get
            '  Return the base class background.
            Return MyBase.BackColor
        End Get
        Set(value As Color)
            '  Set the base class background...
            MyBase.BackColor = value

            '  ...and the internal control background.
            richTextBoxConsole.BackColor = value
        End Set
    End Property

    Public Overrides Property ForeColor As Color
        Get
            Return MyBase.ForeColor
        End Get
        Set(value As Color)
            MyBase.ForeColor = value
            richTextBoxConsole.ForeColor = value
        End Set
    End Property

    ''' <summary>
    ''' Initializes a new instance of the <seecref="ConsoleControl"/> class.
    ''' </summary>
    Public Sub New()
        '  Initialise the component.
        InitializeComponent()

        '  Show diagnostics disabled by default.
        ShowDiagnostics = False
        '  Input enabled by default.
        IsInputEnabled = True
        '  Disable special commands by default.
        SendKeyboardCommandsToProcess = False
        m_console = New ProcessInterface

        '  Initialise the keymappings.
        Call InitialiseKeyMappings()

        '  Suppress the RichTextBox's native right-click context menu so that our
        '  custom right-click paste behaviour can take over. Native Ctrl+C/Ctrl+V
        '  shortcuts are preserved.
        richTextBoxConsole.ContextMenuStrip = New ContextMenuStrip()
    End Sub

    Public Sub SetConsoleCore([interface] As AbstractProcessInterface)
        m_console = [interface]
    End Sub

    Public Function GetInterface() As AbstractProcessInterface
        Return ProcessInterface
    End Function

    ''' <summary>
    ''' Handles the OnProcessError event of the processInterace control.
    ''' </summary>
    ''' <paramname="sender">The source of the event.</param>
    ''' <paramname="args">The <seecref="ProcessEventArgs"/> instance containing the event data.</param>
    Private Sub processInterace_OnProcessError(sender As Object, args As ProcessEventArgs) Handles m_console.OnProcessError
        '  Write the output, in red
        WriteOutput(args.Content, Color.Red)

        '  Fire the output event.
        FireConsoleOutputEvent(args.Content)
    End Sub

    ''' <summary>
    ''' Handles the OnProcessOutput event of the processInterace control.
    ''' </summary>
    ''' <paramname="sender">The source of the event.</param>
    ''' <paramname="args">The <seecref="ProcessEventArgs"/> instance containing the event data.</param>
    Private Sub processInterace_OnProcessOutput(sender As Object, args As ProcessEventArgs) Handles m_console.OnProcessOutput
        '  Write the output, in white
        WriteOutput(args.Content, Color.White)

        '  Fire the output event.
        FireConsoleOutputEvent(args.Content)
    End Sub

    ''' <summary>
    ''' Handles the OnProcessInput event of the processInterace control.
    ''' </summary>
    ''' <paramname="sender">The source of the event.</param>
    ''' <paramname="args">The <seecref="ProcessEventArgs"/> instance containing the event data.</param>
    Private Sub processInterace_OnProcessInput(sender As Object, args As ProcessEventArgs) Handles m_console.OnProcessInput

    End Sub

    Public Event ProcessExisted()

    ''' <summary>
    ''' Handles the OnProcessExit event of the processInterace control.
    ''' </summary>
    ''' <paramname="sender">The source of the event.</param>
    ''' <paramname="args">The <seecref="ProcessEventArgs"/> instance containing the event data.</param>
    Private Sub processInterace_OnProcessExit(sender As Object, args As ProcessEventArgs) Handles m_console.OnProcessExit
        '  Are we showing diagnostics?
        If ShowDiagnostics AndAlso TypeOf ProcessInterface Is ProcessInterface Then
            WriteOutput(Environment.NewLine & DirectCast(m_console, ProcessInterface).ProcessFileName & " exited.", Color.FromArgb(255, 0, 255, 0))
        End If

        If Not IsHandleCreated Then
            Return
        Else
            '  Read only again.
            Invoke(Sub() richTextBoxConsole.ReadOnly = True)
        End If

        RaiseEvent ProcessExisted()
    End Sub

    ''' <summary>
    ''' Initialises the key mappings.
    ''' </summary>
    Private Sub InitialiseKeyMappings()
        '  Map 'tab'.
        KeyMappings.Add(New KeyMapping(False, False, False, Keys.Tab, "{TAB}", vbTab))
        '  Map 'Ctrl-C'.
        KeyMappings.Add(New KeyMapping(True, False, False, Keys.C, "^(c)", ChrW(3) & vbCrLf))
    End Sub

    ''' <summary>
    ''' Handles the KeyDown event of the richTextBoxConsole control.
    ''' </summary>
    ''' <paramname="sender">The source of the event.</param>
    ''' <paramname="e">The <seecref="System.Windows.Forms.KeyEventArgs"/> instance containing the event data.</param>
    Private Sub richTextBoxConsole_KeyDown(sender As Object, e As KeyEventArgs) Handles richTextBoxConsole.KeyDown
        '  Up/Down history navigation. Only when input is enabled, an input line exists and
        '  the caret is inside the input zone (so that scrolling in the read-only history
        '  area is unaffected).
        If m_isInputEnabled AndAlso inputStart >= 0 AndAlso
           richTextBoxConsole.SelectionStart >= inputStart AndAlso
           (e.KeyCode = Keys.Up OrElse e.KeyCode = Keys.Down) Then

            e.SuppressKeyPress = True
            e.Handled = True

            If e.KeyCode = Keys.Up Then
                '  Step back through history (most recent first).
                If historyIndex > 0 Then
                    historyIndex -= 1
                    Call ReplaceInputBuffer(inputHistory(historyIndex))
                End If
            Else
                '  Step forward; once past the last entry, clear the input line.
                If historyIndex < inputHistory.Count Then
                    historyIndex += 1
                    If historyIndex < inputHistory.Count Then
                        Call ReplaceInputBuffer(inputHistory(historyIndex))
                    Else
                        Call ReplaceInputBuffer("")
                    End If
                End If
            End If

            Return
        End If

        '  When input is enabled and there is an active input line, emulate a native
        '  console's single caret: for any key that affects input, move the caret back
        '  to the end of the current input line so typing works from anywhere.
        If m_isInputEnabled AndAlso inputStart >= 0 Then
            Dim inputEnd As Integer = richTextBoxConsole.TextLength

            '  Navigation (arrows) and Ctrl-C copy remain usable in the history area.
            Dim isNavigationKey = e.KeyCode = Keys.Left OrElse
                                  e.KeyCode = Keys.Right OrElse
                                  e.KeyCode = Keys.Up OrElse
                                  e.KeyCode = Keys.Down
            Dim isCopyKey = e.KeyCode = Keys.C AndAlso e.Control

            '  Backspace only counts as an input key when there is something to delete.
            Dim isBackspaceKey = e.KeyCode = Keys.Back
            Dim isInputKey = Not isNavigationKey AndAlso Not isCopyKey AndAlso
                             (Not isBackspaceKey OrElse inputEnd > inputStart)

            If isInputKey AndAlso richTextBoxConsole.SelectionStart <> inputEnd Then
                richTextBoxConsole.SelectionStart = inputEnd
                richTextBoxConsole.SelectionLength = 0
                richTextBoxConsole.ScrollToCaret()
            End If
        End If

        '  Check whether we are in the read-only zone.
        Dim isInReadOnlyZone = richTextBoxConsole.SelectionStart < inputStart

        '  Are we sending keyboard commands to the process?
        If SendKeyboardCommandsToProcess AndAlso IsProcessRunning Then
            '  Get key mappings for this key event?
            Dim mappings = From k As KeyMapping
                           In KeyMappings
                           Where k.KeyCode = e.KeyCode AndAlso
                               k.IsAltPressed = e.Alt AndAlso
                               k.IsControlPressed = e.Control AndAlso
                               k.IsShiftPressed = e.Shift
                           Select k

            '  Go through each mapping, send the message.
            'foreach (var mapping in mappings)
            '{
            'SendKeysEx.SendKeys(CurrentProcessHwnd, mapping.SendKeysMapping);
            'inputWriter.WriteLine(mapping.StreamMapping);
            'WriteInput("\x3", Color.White, false);
            '}

            '  If we handled a mapping, we're done here.
            If mappings.Any() Then
                e.SuppressKeyPress = True
                Return
            End If
        End If

        '  If we're at the input point and it's backspace, bail.
        If richTextBoxConsole.SelectionStart <= inputStart AndAlso e.KeyCode = Keys.Back Then
            e.SuppressKeyPress = True
        End If

        '  Are we in the read-only zone?
        If isInReadOnlyZone Then
            '  Allow arrows and Ctrl-C.
            If Not (e.KeyCode = Keys.Left OrElse
                e.KeyCode = Keys.Right OrElse
                e.KeyCode = Keys.Up OrElse
                e.KeyCode = Keys.Down OrElse
                e.KeyCode = Keys.C AndAlso e.Control) Then

                e.SuppressKeyPress = True
            End If
        End If

        '  Write the input if we hit return and we're NOT in the read only zone.
        If e.KeyCode = Keys.Return AndAlso Not isInReadOnlyZone Then
            '  Get the input.
            Dim strlen As Integer = richTextBoxConsole.SelectionStart - inputStart
            Dim input = richTextBoxConsole.Text.Substring(inputStart, strlen)

            '  Record non-empty input into the history so it can be recalled with the
            '  up/down arrows. Reset the index to the end so the next Up starts from the
            '  most recently submitted command.
            If Not String.IsNullOrEmpty(input) Then
                inputHistory.Add(input)
                historyIndex = inputHistory.Count
            End If

            '  Write the input (without echoing).
            Call WriteInput(input, Color.White, False)
        End If
    End Sub

    ''' <summary>
    ''' Handles the MouseUp event of the richTextBoxConsole control.
    ''' </summary>
    ''' <param name="sender">The source of the event.</param>
    ''' <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
    Private Sub richTextBoxConsole_MouseUp(sender As Object, e As MouseEventArgs) Handles richTextBoxConsole.MouseUp
        '  Right button: if the clipboard holds text, paste it into the input buffer.
        If e.Button = MouseButtons.Right Then
            If Clipboard.ContainsText() Then
                Call InsertIntoInputBuffer(Clipboard.GetText())
            End If
        ElseIf e.Button = MouseButtons.Left AndAlso richTextBoxConsole.SelectionLength > 0 Then
            '  Left button with an active selection: copy the selection to the clipboard
            '  (console-style "select to copy" behaviour, no Enter/Ctrl+C needed).
            richTextBoxConsole.Copy()
        End If
    End Sub

    ''' <summary>
    ''' Inserts the given text into the current input buffer. The text is inserted at the
    ''' caret when the caret is inside the input zone, otherwise it is appended at the end
    ''' of the current input line.
    ''' </summary>
    ''' <param name="text">The text to insert.</param>
    Private Sub InsertIntoInputBuffer(text As String)
        '  No active input line: nothing to insert into.
        If inputStart < 0 Then
            Return
        End If

        '  Move the caret to the end of the input line when it sits in the read-only zone.
        If richTextBoxConsole.SelectionStart < inputStart Then
            richTextBoxConsole.SelectionStart = richTextBoxConsole.TextLength
        End If

        richTextBoxConsole.SelectionLength = 0
        richTextBoxConsole.SelectionColor = Color.White
        richTextBoxConsole.SelectedText = text

        '  Place the caret at the end of the inserted text.
        richTextBoxConsole.SelectionStart = richTextBoxConsole.TextLength
        richTextBoxConsole.SelectionLength = 0
        richTextBoxConsole.ScrollToCaret()
    End Sub

    ''' <summary>
    ''' Replaces the entire current input line with the supplied text and moves the caret
    ''' to the end of the line.
    ''' </summary>
    ''' <param name="newText">The text to place into the input buffer.</param>
    Private Sub ReplaceInputBuffer(newText As String)
        '  No active input line: nothing to replace.
        If inputStart < 0 Then
            Return
        End If

        richTextBoxConsole.SelectionStart = inputStart
        richTextBoxConsole.SelectionLength = richTextBoxConsole.TextLength - inputStart
        richTextBoxConsole.SelectionColor = Color.White
        richTextBoxConsole.SelectedText = newText

        '  Place the caret at the end of the replaced line.
        richTextBoxConsole.SelectionStart = richTextBoxConsole.TextLength
        richTextBoxConsole.SelectionLength = 0
        richTextBoxConsole.ScrollToCaret()
    End Sub

    ''' <summary>
    ''' Writes the output to the console control.
    ''' </summary>
    ''' <paramname="output">The output.</param>
    ''' <paramname="color">The color.</param>
    Public Sub WriteOutput(output As String, color As Color)
        If lastInput.StringEmpty = False AndAlso (Equals(output, lastInput) OrElse Equals(output.Replace(vbCrLf, ""), lastInput)) Then
            Return
        End If
        If Not IsHandleCreated Then
            Return
        End If

        Invoke(Sub()
                   '  Always append at the end of the content, regardless of where the
                   '  caret/selection currently is (matches native console behaviour).
                   richTextBoxConsole.SelectionStart = richTextBoxConsole.TextLength
                   richTextBoxConsole.SelectionLength = 0
                   richTextBoxConsole.SelectionColor = color
                   richTextBoxConsole.AppendText(output)
                   inputStart = richTextBoxConsole.TextLength
                   richTextBoxConsole.ScrollToCaret()
               End Sub)
    End Sub

    Public Sub WriteAnsiEscape(ansiText As String)
        Call AnsiEscapeRenderer.RenderAnsiText(richTextBoxConsole, ansiText)
    End Sub

    ''' <summary>
    ''' Clears the output.
    ''' </summary>
    Public Sub ClearOutput()
        richTextBoxConsole.Clear()
        inputStart = 0
    End Sub

    ''' <summary>
    ''' Writes the input to the console control.
    ''' </summary>
    ''' <paramname="input">The input.</param>
    ''' <paramname="color">The color.</param>
    ''' <paramname="echo">if set to <c>true</c> echo the input.</param>
    Public Sub WriteInput(input As String, color As Color, echo As Boolean)
        Invoke(Sub()
                   '  Are we echoing?
                   If echo Then
                       richTextBoxConsole.SelectionStart = richTextBoxConsole.TextLength
                       richTextBoxConsole.SelectionLength = 0
                       richTextBoxConsole.SelectionColor = color
                       richTextBoxConsole.AppendText(input)
                       inputStart = richTextBoxConsole.TextLength
                       richTextBoxConsole.ScrollToCaret()
                   End If

                   lastInput = input
                   '  Write the input.
                   m_console.WriteInput(input)

                   '  Fire the event.
                   FireConsoleInputEvent(input)
               End Sub)
    End Sub

    ''' <summary>
    ''' Starts the underlying session/process using the parameterless contract.
    ''' This is used by back-ends (such as an SSH shell) whose connection
    ''' parameters are configured out-of-band (e.g. via dedicated properties or
    ''' a Connect() call). It does not change the behaviour of the existing
    ''' file-name based overloads used by the local console.
    ''' </summary>
    Public Sub StartProcess()
        '  Are we showing diagnostics?
        If ShowDiagnostics Then
            WriteOutput("Starting session..." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
        End If

        '  Start the back-end (no arguments needed).
        m_console.StartProcess()

        '  If we enable input, make the control not read only.
        If IsInputEnabled Then
            richTextBoxConsole.ReadOnly = False
        End If
    End Sub

    ''' <summary>
    ''' Runs a process.
    ''' </summary>
    ''' <paramname="fileName">Name of the file.</param>
    ''' <paramname="arguments">The arguments.</param>
    Public Sub StartProcess(fileName As String, arguments As String)
        '  Are we showing diagnostics?
        If ShowDiagnostics Then
            WriteOutput("Preparing to run " & fileName, Color.FromArgb(255, 0, 255, 0))
            If Not String.IsNullOrEmpty(arguments) Then
                WriteOutput(" with arguments " & arguments & "." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
            Else
                WriteOutput("." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
            End If
        End If

        If TypeOf ProcessInterface Is ProcessInterface Then
            '  Start the process.
            Call DirectCast(m_console, ProcessInterface).StartProcess(fileName, arguments)

            '  If we enable input, make the control not read only.
            If IsInputEnabled Then
                richTextBoxConsole.ReadOnly = False
            End If
        Else
            Call "Can not start external process".warning
        End If
    End Sub

    ''' <summary>
    ''' Runs a process.
    ''' </summary>
    ''' <paramname="processStartInfo"><seecref="ProcessStartInfo"/> to pass to the process.</param>
    Public Sub StartProcess(processStartInfo As ProcessStartInfo)
        '  Are we showing diagnostics?
        If ShowDiagnostics Then
            WriteOutput("Preparing to run " & processStartInfo.FileName, Color.FromArgb(255, 0, 255, 0))
            If Not String.IsNullOrEmpty(processStartInfo.Arguments) Then
                WriteOutput(" with arguments " & processStartInfo.Arguments & "." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
            Else
                WriteOutput("." & Environment.NewLine, Color.FromArgb(255, 0, 255, 0))
            End If
        End If

        If TypeOf ProcessInterface Is ProcessInterface Then
            '  Start the process.
            Call DirectCast(m_console, ProcessInterface).StartProcess(processStartInfo)

            '  If we enable input, make the control not read only.
            If IsInputEnabled Then
                richTextBoxConsole.ReadOnly = False
            End If
        End If
    End Sub

    ''' <summary>
    ''' Stops the process.
    ''' </summary>
    Public Sub StopProcess()
        '  Stop the back-end via the (possibly overridden) contract.
        Call m_console.StopProcess()
    End Sub

    ''' <summary>
    ''' Fires the console output event.
    ''' </summary>
    ''' <paramname="content">The content.</param>
    Private Sub FireConsoleOutputEvent(content As String)
        '  Get the event.
        RaiseEvent OnConsoleOutput(Me, New ConsoleEventArgs(content))
    End Sub

    ''' <summary>
    ''' Fires the console input event.
    ''' </summary>
    ''' <paramname="content">The content.</param>
    Private Sub FireConsoleInputEvent(content As String)
        '  Get the event.
        RaiseEvent OnConsoleInput(Me, New ConsoleEventArgs(content))
    End Sub
End Class
