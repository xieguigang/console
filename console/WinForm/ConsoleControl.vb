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
    Dim WithEvents m_console As AbstractProcessInterface

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

            '  Write the input (without echoing).
            Call WriteInput(input, Color.White, False)
        End If
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
                   '  Write the output.
                   richTextBoxConsole.SelectionColor = color
                   richTextBoxConsole.SelectedText &= output
                   inputStart = richTextBoxConsole.SelectionStart
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
                       richTextBoxConsole.SelectionColor = color
                       richTextBoxConsole.SelectedText &= input
                       inputStart = richTextBoxConsole.SelectionStart
                   End If

                   lastInput = input
                   '  Write the input.
                   m_console.WriteInput(input)

                   '  Fire the event.
                   FireConsoleInputEvent(input)
               End Sub)
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
        If TypeOf ProcessInterface Is ProcessInterface Then
            '  Stop the interface.
            Call DirectCast(m_console, ProcessInterface).StopProcess()
        End If
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
            If TypeOf ProcessInterface Is ProcessInterface Then
                Return DirectCast(m_console, ProcessInterface).IsProcessRunning
            End If

            Return False
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
End Class
