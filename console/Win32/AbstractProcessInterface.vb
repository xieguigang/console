Imports System.ComponentModel
Imports System.IO
Imports System.Text
Imports System.Threading

Namespace Win32

    ''' <summary>
    ''' Describes how a console front-end should feed keystrokes to a back-end.
    ''' </summary>
    Public Enum ConsoleInputMode
        ''' <summary>
        ''' Line editing: the renderer echoes locally, handles in-line editing and
        ''' history, and only submits a whole line once Enter is pressed. Suitable
        ''' for back-ends that have no PTY of their own (a local command shell).
        ''' </summary>
        LineEdit = 0

        ''' <summary>
        ''' Raw pass-through: every keystroke is forwarded to the back-end as it
        ''' happens. Suitable for back-ends that drive a real PTY (an SSH shell).
        ''' </summary>
        Raw = 1
    End Enum

    ''' <summary>
    ''' Implemented by back-ends that need to know what the user has typed but not
    ''' yet submitted, so they can act on it when a raw key arrives - tab
    ''' completion being the motivating case.
    ''' </summary>
    ''' <remarks>
    ''' Optional: the console probes for it with <c>TryCast</c>, so back-ends that
    ''' do not implement it are unaffected.
    ''' </remarks>
    Public Interface IEditableInputLine

        ''' <summary>
        ''' Reports the renderer's current line buffer and caret offset, pushed
        ''' immediately before the raw keystroke that triggered it.
        ''' </summary>
        Sub SetEditorState(line As String, cursorPosition As Integer)

    End Interface

    Public MustInherit Class AbstractProcessInterface : Implements IDisposable

        ''' <summary>
        ''' The input writer.
        ''' </summary>
        Protected inputWriter As StreamWriter

        ''' <summary>
        ''' The output reader.
        ''' </summary>
        Protected outputReader As TextReader

        ''' <summary>
        ''' The error reader.
        ''' </summary>
        Protected errorReader As TextReader

        ''' <summary>
        ''' The output worker.
        ''' </summary>
        Protected WithEvents outputWorker As New BackgroundWorker()

        ''' <summary>
        ''' The error worker.
        ''' </summary>
        Protected WithEvents errorWorker As New BackgroundWorker()

        ''' <summary>
        ''' Occurs when process output is produced.
        ''' </summary>
        Public Event OnProcessOutput(sender As Object, args As ProcessEventArgs)

        ''' <summary>
        ''' Occurs when process error output is produced.
        ''' </summary>
        Public Event OnProcessError(sender As Object, args As ProcessEventArgs)

        ''' <summary>
        ''' Occurs when process input is produced.
        ''' </summary>
        Public Event OnProcessInput(sender As Object, args As ProcessEventArgs)

        Public Event OnProcessExit(sender As Object, args As ProcessEventArgs)

        ''' <summary>
        ''' Raised when the back-end wants the front-end's editable input line to
        ''' be replaced wholesale - for example after tab completion rewrote it.
        ''' Only meaningful while the console runs in
        ''' <see cref="ConsoleInputMode.LineEdit"/>.
        ''' </summary>
        Public Event OnSetInputLine(sender As Object, args As ProcessEventArgs)

        Protected ansi As Boolean = False

        ''' <summary>
        ''' Gets a value indicating whether the underlying session/process is running.
        ''' The default implementation returns <c>false</c>; concrete back-ends
        ''' (local process, SSH session, etc.) override this.
        ''' </summary>
        Public Overridable ReadOnly Property IsProcessRunning As Boolean
            Get
                Return False
            End Get
        End Property

        ''' <summary>
        ''' The input mode this back-end expects the hosting console to use.
        ''' Defaults to <see cref="ConsoleInputMode.LineEdit"/>, which is the safe
        ''' choice for back-ends without a PTY; override it to
        ''' <see cref="ConsoleInputMode.Raw"/> when the back-end wants keystrokes
        ''' forwarded verbatim.
        ''' </summary>
        Public Overridable ReadOnly Property PreferredInputMode As ConsoleInputMode
            Get
                Return ConsoleInputMode.LineEdit
            End Get
        End Property

        ''' <summary>
        ''' Starts the session/process. The parameterless overload is used by
        ''' back-ends (such as an SSH shell) that already have their connection
        ''' parameters configured out-of-band. Concrete back-ends override it.
        ''' </summary>
        Public Overridable Sub StartProcess()
        End Sub

        ''' <summary>
        ''' Stops the session/process. Concrete back-ends override it.
        ''' </summary>
        Public Overridable Sub StopProcess()
        End Sub

        ''' <summary>
        ''' Raises the <see cref="OnProcessOutput"/> event. Provided so derived
        ''' back-ends (which cannot <c>RaiseEvent</c> a base-class event directly)
        ''' can push remote output to the hosting console.
        ''' </summary>
        Protected Sub RaiseOutputEvent(text As String)
            RaiseEvent OnProcessOutput(Me, New ProcessEventArgs(text, ansi:=ansi))
        End Sub

        ''' <summary>
        ''' Raises the <see cref="OnProcessError"/> event.
        ''' </summary>
        Protected Sub RaiseErrorEvent(text As String)
            RaiseEvent OnProcessError(Me, New ProcessEventArgs(text))
        End Sub

        ''' <summary>
        ''' Raises the <see cref="OnSetInputLine"/> event, asking the front-end to
        ''' replace whatever the user has typed so far with <paramref name="text"/>.
        ''' </summary>
        Protected Sub RaiseSetInputLineEvent(text As String)
            RaiseEvent OnSetInputLine(Me, New ProcessEventArgs(If(text, String.Empty)))
        End Sub

        ''' <summary>
        ''' Raises the <see cref="OnProcessExit"/> event.
        ''' </summary>
        Protected Sub RaiseExitEvent()
            RaiseEvent OnProcessExit(Me, New ProcessEventArgs(String.Empty))
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <see cref="ProcessInterface"/> class.
        ''' </summary>
        Protected Sub New(void As Object)
            '  Configure the output worker.
            outputWorker.WorkerReportsProgress = True
            outputWorker.WorkerSupportsCancellation = True

            '  Configure the error worker.
            errorWorker.WorkerReportsProgress = True
            errorWorker.WorkerSupportsCancellation = True
        End Sub

        ''' <summary>
        ''' Fires the process exit event.
        ''' </summary>
        ''' <param name="code">The code.</param>
        Protected Sub FireProcessExitEvent(code As Integer)
            '  Get the event and fire it.
            RaiseEvent OnProcessExit(Me, New ProcessEventArgs(code))
        End Sub

        Public MustOverride Sub WriteInput(input As String)

        ''' <summary>
        ''' Writes raw input to the underlying session/process without appending
        ''' any line terminator. Used to send control signals such as Ctrl+C
        ''' (ETX, <c>ChrW(3)</c>) that must not be corrupted by an extra newline.
        ''' Concrete back-ends override this.
        ''' </summary>
        ''' <param name="input">The raw input to send.</param>
        Public MustOverride Sub WriteRaw(input As String)

        ''' <summary>
        ''' Handles the ProgressChanged event of the outputWorker control.
        ''' </summary>
        ''' <param name="sender">The source of the event.</param>
        ''' <param name="e">The <see cref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
        Private Sub outputWorker_ProgressChanged(sender As Object, e As ProgressChangedEventArgs) Handles outputWorker.ProgressChanged
            '  We must be passed a string in the user state.
            If TypeOf e.UserState Is String Then
                '  Fire the output event.
                RaiseEvent OnProcessOutput(Me, New ProcessEventArgs(TryCast(e.UserState, String)))
            End If
        End Sub

        ''' <summary>
        ''' Handles the DoWork event of the outputWorker control.
        ''' </summary>
        ''' <param name="sender">The source of the event.</param>
        ''' <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        Private Sub outputWorker_DoWork(sender As Object, e As DoWorkEventArgs) Handles outputWorker.DoWork
            While outputWorker.CancellationPending = False
                '  Any lines to read?
                Dim count As Integer
                Dim buffer = New Char(1023) {}
                Do
                    Dim builder = New StringBuilder()
                    count = outputReader.Read(buffer, 0, 1024)
                    builder.Append(buffer, 0, count)
                    outputWorker.ReportProgress(0, builder.ToString())
                Loop While count > 0

                Call Thread.Sleep(200)
            End While
        End Sub

        ''' <summary>
        ''' Handles the ProgressChanged event of the errorWorker control.
        ''' </summary>
        ''' <param name="sender">The source of the event.</param>
        ''' <param name="e">The <see cref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
        Private Sub errorWorker_ProgressChanged(sender As Object, e As ProgressChangedEventArgs) Handles errorWorker.ProgressChanged
            '  The userstate must be a string.
            If TypeOf e.UserState Is String Then
                '  Fire the error event.
                RaiseEvent OnProcessError(Me, New ProcessEventArgs(TryCast(e.UserState, String)))
            End If
        End Sub

        ''' <summary>
        ''' Handles the DoWork event of the errorWorker control.
        ''' </summary>
        ''' <param name="sender">The source of the event.</param>
        ''' <param name="e">The <see cref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        Private Sub errorWorker_DoWork(sender As Object, e As DoWorkEventArgs) Handles errorWorker.DoWork
            While errorWorker.CancellationPending = False
                '  Any lines to read?
                Dim count As Integer
                Dim buffer = New Char(1023) {}
                Do
                    Dim builder = New StringBuilder()
                    count = errorReader.Read(buffer, 0, 1024)
                    builder.Append(buffer, 0, count)
                    errorWorker.ReportProgress(0, builder.ToString())
                Loop While count > 0

                Call Thread.Sleep(200)
            End While
        End Sub

        Private disposedValue As Boolean

        Protected Overridable Sub Dispose(disposing As Boolean)
            If Not disposedValue Then
                If disposing Then
                    ' TODO: dispose managed state (managed objects)
                    Call ShutdownInternal()
                End If

                ' TODO: free unmanaged resources (unmanaged objects) and override finalizer
                ' TODO: set large fields to null
                disposedValue = True
            End If
        End Sub

        Private Sub ShutdownInternal()
            On Error Resume Next

            If outputWorker IsNot Nothing Then
                outputWorker.Dispose()
                outputWorker = Nothing
            End If
            If errorWorker IsNot Nothing Then
                errorWorker.Dispose()
                errorWorker = Nothing
            End If
            If inputWriter IsNot Nothing Then
                inputWriter.Dispose()
                inputWriter = Nothing
            End If
            If outputReader IsNot Nothing Then
                outputReader.Dispose()
                outputReader = Nothing
            End If
            If errorReader IsNot Nothing Then
                errorReader.Dispose()
                errorReader = Nothing
            End If
        End Sub

        ' ' TODO: override finalizer only if 'Dispose(disposing As Boolean)' has code to free unmanaged resources
        ' Protected Overrides Sub Finalize()
        '     ' Do not change this code. Put cleanup code in 'Dispose(disposing As Boolean)' method
        '     Dispose(disposing:=False)
        '     MyBase.Finalize()
        ' End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            ' Do not change this code. Put cleanup code in 'Dispose(disposing As Boolean)' method
            Dispose(disposing:=True)
            GC.SuppressFinalize(Me)
        End Sub
    End Class
End Namespace