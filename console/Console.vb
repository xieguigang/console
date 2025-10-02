Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.ApplicationServices.Terminal.STDIO__
Imports Microsoft.VisualBasic.Windows.Forms.Win32

Public Class Console : Inherits AbstractProcessInterface
    Implements IDisposable, IConsole, IShellDevice



    Public Sub New()
        MyBase.New(void:=Nothing)

        inputWriter =
    End Sub

    Public ReadOnly Property WindowWidth As Integer Implements IConsole.WindowWidth
    Public Property BackgroundColor As ConsoleColor Implements IConsole.BackgroundColor
    Public Property ForegroundColor As ConsoleColor Implements IConsole.ForegroundColor

    Public Event Tab As IConsole.TabEventHandler Implements IConsole.Tab

    Public Sub Clear() Implements IWriteDevice.Clear
    End Sub

    Public Sub Write(str As String) Implements IWriteDevice.Write
        Throw New NotImplementedException()
    End Sub

    Public Sub WriteLine() Implements IWriteDevice.WriteLine
        Throw New NotImplementedException()
    End Sub

    Public Sub WriteLine(str As String) Implements IWriteDevice.WriteLine
        Throw New NotImplementedException()
    End Sub

    Public Sub WriteLine(s As String, ParamArray args() As Object) Implements IWriteDevice.WriteLine
        Throw New NotImplementedException()
    End Sub

    Public Sub SetPrompt(s As String) Implements IShellDevice.SetPrompt
        Throw New NotImplementedException()
    End Sub

    Public Function ReadLine() As String Implements IReadDevice.ReadLine
        Throw New NotImplementedException()
    End Function

    Public Function Read() As Integer Implements IReadDevice.Read
        Throw New NotImplementedException()
    End Function

    Public Function ReadKey() As ConsoleKeyInfo Implements IReadDevice.ReadKey
        Throw New NotImplementedException()
    End Function

    Private Function IShellDevice_ReadLine() As String Implements IShellDevice.ReadLine
        Return ReadLine()
    End Function
End Class
