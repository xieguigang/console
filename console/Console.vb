Imports System.IO
Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.ApplicationServices.Terminal.STDIO__
Imports Microsoft.VisualBasic.Windows.Forms.Win32

Public Class Console : Inherits AbstractProcessInterface
    Implements IDisposable, IConsole, IShellDevice

    Dim stdin As New MemoryStream
    Dim stdout As New MemoryStream
    Dim stderr As New MemoryStream

    Public Sub New()
        MyBase.New(void:=Nothing)

        inputWriter = New StreamWriter(stdin)
        outputReader = New StreamReader(stdout)
        errorReader = New StreamReader(stderr)
    End Sub

    Public ReadOnly Property WindowWidth As Integer Implements IConsole.WindowWidth
    Public Property BackgroundColor As ConsoleColor Implements IConsole.BackgroundColor
    Public Property ForegroundColor As ConsoleColor Implements IConsole.ForegroundColor

    Public Event Tab As IConsole.TabEventHandler Implements IConsole.Tab
    Public Event CancelKeyPress()

    Public Sub Clear() Implements IWriteDevice.Clear
    End Sub

    Public Sub Write(str As String) Implements IWriteDevice.Write

    End Sub

    Public Sub WriteLine() Implements IWriteDevice.WriteLine
        Call Write(vbLf)
    End Sub

    Public Sub WriteLine(str As String) Implements IWriteDevice.WriteLine
        Call Write(str & vbLf)
    End Sub

    Public Sub WriteLine(s As String, ParamArray args() As Object) Implements IWriteDevice.WriteLine
        Call WriteLine(String.Format(s, args))
    End Sub

    Public Function ReadLine() As String Implements IReadDevice.ReadLine, IShellDevice.ReadLine

    End Function

    Public Function Read() As Integer Implements IReadDevice.Read

    End Function

    Public Function ReadKey() As ConsoleKeyInfo Implements IReadDevice.ReadKey

    End Function

    ''' <summary>
    ''' set ps1
    ''' </summary>
    ''' <param name="s"></param>
    Public Sub SetPrompt(s As String) Implements IShellDevice.SetPrompt
        Throw New NotImplementedException()
    End Sub
End Class
