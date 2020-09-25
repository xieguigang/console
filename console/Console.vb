Imports Microsoft.VisualBasic.ApplicationServices.Terminal.xConsole

Public Class Console : Implements IDisposable

    Friend ReadOnly device As ConsoleControl
    Friend ReadOnly sharedStream As New PipelineStream

    Public Property BackgroundColor As ConsoleColor
        Get
            Dim cl As Color = device.BackColor
            Dim enumCl As ConsoleColor = xConsole.ClosestConsoleColor(cl.R, cl.G, cl.B)

            Return enumCl
        End Get
        Set(value As ConsoleColor)
            device.BackColor = Internal.FromConsoleColor(value.ToString)
        End Set
    End Property

    Public Property ForegroundColor As ConsoleColor
        Get
            Dim cl As Color = device.ForeColor
            Dim enumCl As ConsoleColor = xConsole.ClosestConsoleColor(cl.R, cl.G, cl.B)

            Return enumCl
        End Get
        Set(value As ConsoleColor)
            device.ForeColor = Internal.FromConsoleColor(value.ToString)
        End Set
    End Property

    Sub New(dev As ConsoleControl)
        device = dev
    End Sub

    Public Sub SetConsoleForeColor(color As Color)
        device.ForeColor = color
    End Sub

    Public Sub SetConsoleBackColor(color As Color)
        device.BackColor = color
    End Sub

    Public Function ReadLine() As String
        Return sharedStream.GetLine
    End Function

    Public Function ReadKey() As Char
        Return sharedStream.GetChar
    End Function

    Public Sub Write(str As String)
        Call device.Invoke(Sub() device.write(str))
    End Sub

    Public Sub WriteLine(str As String)
        Call device.Invoke(Sub() device.write(str & vbCr))
    End Sub

    Protected Overridable Sub Dispose(disposing As Boolean)
        If Not disposedValue Then
            If disposing Then
                ' TODO: dispose managed state (managed objects)
            End If

            ' TODO: free unmanaged resources (unmanaged objects) and override finalizer
            ' TODO: set large fields to null
            disposedValue = True
        End If
    End Sub

    ' ' TODO: override finalizer only if 'Dispose(disposing As Boolean)' has code to free unmanaged resources
    ' Protected Overrides Sub Finalize()
    '     ' Do not change this code. Put cleanup code in 'Dispose(disposing As Boolean)' method
    '     Dispose(disposing:=False)
    '     MyBase.Finalize()
    ' End Sub

    Private disposedValue As Boolean

    Public Sub Dispose() Implements IDisposable.Dispose
        ' Do not change this code. Put cleanup code in 'Dispose(disposing As Boolean)' method
        Dispose(disposing:=True)
        GC.SuppressFinalize(Me)
    End Sub
End Class
