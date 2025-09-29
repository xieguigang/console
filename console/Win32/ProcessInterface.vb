Imports System.ComponentModel
Imports System.IO
Imports System.Text
Imports System.Threading

Namespace Win32

    ''' <summary>
    ''' A ProcessEventHandler is a delegate for process input/output events.
    ''' </summary>
    ''' <paramname="sender">The sender.</param>
    ''' <paramname="args">The <seecref="ProcessEventArgs"/> instance containing the event data.</param>
    Public Delegate Sub ProcessEventHandler(sender As Object, args As ProcessEventArgs)

    ''' <summary>
    ''' A class the wraps a process, allowing programmatic input and output.
    ''' </summary>
    Public Class ProcessInterface : Implements IDisposable

        ''' <summary>
        ''' The input writer.
        ''' </summary>
        Private inputWriter As StreamWriter

        ''' <summary>
        ''' The output reader.
        ''' </summary>
        Private outputReader As TextReader

        ''' <summary>
        ''' The error reader.
        ''' </summary>
        Private errorReader As TextReader

        ''' <summary>
        ''' The output worker.
        ''' </summary>
        Private outputWorker As BackgroundWorker = New BackgroundWorker()

        ''' <summary>
        ''' The error worker.
        ''' </summary>
        Private errorWorker As BackgroundWorker = New BackgroundWorker()

        ''' <summary>
        ''' Occurs when process output is produced.
        ''' </summary>
        Public Event OnProcessOutput As ProcessEventHandler

        ''' <summary>
        ''' Occurs when process error output is produced.
        ''' </summary>
        Public Event OnProcessError As ProcessEventHandler

        ''' <summary>
        ''' Occurs when process input is produced.
        ''' </summary>
        Public Event OnProcessInput As ProcessEventHandler

        ''' <summary>
        ''' Occurs when the process ends.
        ''' </summary>
        Public Event OnProcessExit As ProcessEventHandler

        ''' <summary>
        ''' Gets a value indicating whether this instance is process running.
        ''' </summary>
        ''' <value>
        ''' 	<c>true</c> if this instance is process running; otherwise, <c>false</c>.
        ''' </value>
        Public ReadOnly Property IsProcessRunning As Boolean
            Get
                Try
                    Return Process IsNot Nothing AndAlso Process.HasExited = False
                Catch
                    Return False
                End Try
            End Get
        End Property

        ''' <summary>
        ''' Gets the internal process.
        ''' </summary>
        Public ReadOnly Property Process As Process

        ''' <summary>
        ''' Gets the name of the process.
        ''' </summary>
        ''' <value>
        ''' The name of the process.
        ''' </value>
        Public ReadOnly Property ProcessFileName As String

        ''' <summary>
        ''' Gets the process arguments.
        ''' </summary>
        Public ReadOnly Property ProcessArguments As String

        ''' <summary>
        ''' Initializes a new instance of the <seecref="ProcessInterface"/> class.
        ''' </summary>
        Public Sub New()
            '  Configure the output worker.
            outputWorker.WorkerReportsProgress = True
            outputWorker.WorkerSupportsCancellation = True
            AddHandler outputWorker.DoWork, AddressOf outputWorker_DoWork
            AddHandler outputWorker.ProgressChanged, AddressOf outputWorker_ProgressChanged

            '  Configure the error worker.
            errorWorker.WorkerReportsProgress = True
            errorWorker.WorkerSupportsCancellation = True
            AddHandler errorWorker.DoWork, AddressOf errorWorker_DoWork
            AddHandler errorWorker.ProgressChanged, AddressOf errorWorker_ProgressChanged
        End Sub

        ''' <summary>
        ''' Handles the ProgressChanged event of the outputWorker control.
        ''' </summary>
        ''' <paramname="sender">The source of the event.</param>
        ''' <paramname="e">The <seecref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
        Private Sub outputWorker_ProgressChanged(sender As Object, e As ProgressChangedEventArgs)
            '  We must be passed a string in the user state.
            If TypeOf e.UserState Is String Then
                '  Fire the output event.
                FireProcessOutputEvent(TryCast(e.UserState, String))
            End If
        End Sub

        ''' <summary>
        ''' Handles the DoWork event of the outputWorker control.
        ''' </summary>
        ''' <paramname="sender">The source of the event.</param>
        ''' <paramname="e">The <seecref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        Private Sub outputWorker_DoWork(sender As Object, e As DoWorkEventArgs)
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

                Threading.Thread.Sleep(200)
            End While
        End Sub

        ''' <summary>
        ''' Handles the ProgressChanged event of the errorWorker control.
        ''' </summary>
        ''' <paramname="sender">The source of the event.</param>
        ''' <paramname="e">The <seecref="System.ComponentModel.ProgressChangedEventArgs"/> instance containing the event data.</param>
        Private Sub errorWorker_ProgressChanged(sender As Object, e As ProgressChangedEventArgs)
            '  The userstate must be a string.
            If TypeOf e.UserState Is String Then
                '  Fire the error event.
                FireProcessErrorEvent(TryCast(e.UserState, String))
            End If
        End Sub

        ''' <summary>
        ''' Handles the DoWork event of the errorWorker control.
        ''' </summary>
        ''' <paramname="sender">The source of the event.</param>
        ''' <paramname="e">The <seecref="System.ComponentModel.DoWorkEventArgs"/> instance containing the event data.</param>
        Private Sub errorWorker_DoWork(sender As Object, e As DoWorkEventArgs)
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

                Threading.Thread.Sleep(200)
            End While
        End Sub

        ''' <summary>
        ''' Runs a process.
        ''' </summary>
        ''' <paramname="fileName">Name of the file.</param>
        ''' <paramname="arguments">The arguments.</param>
        Public Sub StartProcess(fileName As String, arguments As String)
            '  Create the process start info.
            StartProcess(New ProcessStartInfo(fileName, arguments))
        End Sub

        ''' <summary>
        ''' Runs a process.
        ''' </summary>
        ''' <paramname="processStartInfo"><seecref="ProcessStartInfo"/> to pass to the process.</param>
        Public Sub StartProcess(processStartInfo As ProcessStartInfo)
            '  Set the options.
            processStartInfo.UseShellExecute = False
            processStartInfo.ErrorDialog = False
            processStartInfo.CreateNoWindow = True

            '  Specify redirection.
            processStartInfo.RedirectStandardError = True
            processStartInfo.RedirectStandardInput = True
            processStartInfo.RedirectStandardOutput = True

            '  Create the process.
            _Process = New Process()
            _Process.EnableRaisingEvents = True
            _Process.StartInfo = processStartInfo
            AddHandler Process.Exited, AddressOf currentProcess_Exited

            '  Start the process.
            Try
                Call Process.Start()
            Catch e As Exception
                '  Trace the exception.
                Trace.WriteLine("Failed to start process " & processStartInfo.FileName & " with arguments '" & processStartInfo.Arguments & "'")
                Call Trace.WriteLine(e.ToString())
                Return
            End Try

            '  Store name and arguments.
            _ProcessFileName = processStartInfo.FileName
            _ProcessArguments = processStartInfo.Arguments

            '  Create the readers and writers.
            inputWriter = Process.StandardInput
            outputReader = TextReader.Synchronized(Process.StandardOutput)
            errorReader = TextReader.Synchronized(Process.StandardError)

            '  Run the workers that read output and error.
            outputWorker.RunWorkerAsync()
            errorWorker.RunWorkerAsync()
        End Sub

        ''' <summary>
        ''' Stops the process.
        ''' </summary>
        Public Sub StopProcess()
            '  Handle the trivial case.
            If IsProcessRunning = False Then Return

            '  Kill the process.
            Process.Kill()
        End Sub

        ''' <summary>
        ''' Handles the Exited event of the currentProcess control.
        ''' </summary>
        ''' <paramname="sender">The source of the event.</param>
        ''' <paramname="e">The <seecref="System.EventArgs"/> instance containing the event data.</param>
        Private Sub currentProcess_Exited(sender As Object, e As EventArgs)
            Dim exitCode As Integer = Process.ExitCode

            '  Disable the threads.
            outputWorker.CancelAsync()
            errorWorker.CancelAsync()
            inputWriter = Nothing
            outputReader = Nothing
            errorReader = Nothing

            _Process = Nothing
            _ProcessFileName = Nothing
            _ProcessArguments = Nothing

            Call Thread.Sleep(1000)

            '  Fire process exited.
            FireProcessExitEvent(exitCode)
        End Sub

        ''' <summary>
        ''' Fires the process output event.
        ''' </summary>
        ''' <paramname="content">The content.</param>
        Private Sub FireProcessOutputEvent(content As String)
            '  Get the event and fire it.
            Dim theEvent = OnProcessOutputEvent
            If theEvent IsNot Nothing Then theEvent(Me, New ProcessEventArgs(content))
        End Sub

        ''' <summary>
        ''' Fires the process error output event.
        ''' </summary>
        ''' <paramname="content">The content.</param>
        Private Sub FireProcessErrorEvent(content As String)
            '  Get the event and fire it.
            Dim theEvent = OnProcessErrorEvent
            If theEvent IsNot Nothing Then theEvent(Me, New ProcessEventArgs(content))
        End Sub

        ''' <summary>
        ''' Fires the process input event.
        ''' </summary>
        ''' <paramname="content">The content.</param>
        Private Sub FireProcessInputEvent(content As String)
            '  Get the event and fire it.
            Dim theEvent = OnProcessInputEvent
            If theEvent IsNot Nothing Then theEvent(Me, New ProcessEventArgs(content))
        End Sub

        ''' <summary>
        ''' Fires the process exit event.
        ''' </summary>
        ''' <paramname="code">The code.</param>
        Private Sub FireProcessExitEvent(code As Integer)
            '  Get the event and fire it.
            Dim theEvent = OnProcessExitEvent
            If theEvent IsNot Nothing Then theEvent(Me, New ProcessEventArgs(code))
        End Sub

        ''' <summary>
        ''' Writes the input.
        ''' </summary>
        ''' <paramname="input">The input.</param>
        Public Sub WriteInput(input As String)
            If IsProcessRunning Then
                inputWriter.WriteLine(input)
                inputWriter.Flush()
            End If
        End Sub

        ''' <summary>Finalizes an instance of the <seecref="ProcessInterface"/> class.</summary>
        Protected Overrides Sub Finalize()
            Dispose(True)
        End Sub

        ''' <summary>Releases unmanaged and - optionally - managed resources.</summary>
        ''' <paramname="native">
        '''   <c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        Protected Sub Dispose(native As Boolean)
            Try
                If outputWorker IsNot Nothing Then
                    outputWorker.Dispose()
                    outputWorker = Nothing
                End If
                If errorWorker IsNot Nothing Then
                    errorWorker.Dispose()
                    errorWorker = Nothing
                End If
                If Process IsNot Nothing Then
                    _Process.Dispose()
                    _Process = Nothing
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
            Catch ex As Exception

            End Try
        End Sub

        ''' <summary>Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.</summary>
        Public Sub Dispose() Implements IDisposable.Dispose
            Dispose(True)
            GC.SuppressFinalize(Me)
        End Sub
    End Class
End Namespace
