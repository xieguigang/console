Imports System.IO
Imports System.Threading

Namespace Win32

    ''' <summary>
    ''' A class the wraps a process, allowing programmatic input and output.
    ''' </summary>
    Public Class ProcessInterface : Inherits AbstractProcessInterface
        Implements IDisposable

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
        ''' Occurs when the process ends.
        ''' </summary>
        Public Event OnProcessExit(sender As Object, args As ProcessEventArgs)

        Sub New()
            Call MyBase.New(void:=Nothing)
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
            If IsProcessRunning = False Then
                Return
            End If

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
        ''' Fires the process exit event.
        ''' </summary>
        ''' <paramname="code">The code.</param>
        Private Sub FireProcessExitEvent(code As Integer)
            '  Get the event and fire it.
            RaiseEvent OnProcessExit(Me, New ProcessEventArgs(code))
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
        Protected Overrides Sub Dispose(native As Boolean)
            Call MyBase.Dispose(native)

            Try
                If Process IsNot Nothing Then
                    _Process.Dispose()
                    _Process = Nothing
                End If
            Catch ex As Exception

            End Try
        End Sub
    End Class
End Namespace
