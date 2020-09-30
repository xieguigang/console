Imports Microsoft.VisualBasic.ApplicationServices.Terminal.STDIO__
Imports Microsoft.VisualBasic.ApplicationServices.Terminal.xConsole

Public Class Console : Implements IDisposable, IConsole

    Friend ReadOnly device As ConsoleControl
    Friend ReadOnly sharedStream As New PipelineStream

    Public Property BackgroundColor As ConsoleColor Implements IConsole.BackgroundColor
        Get
            Dim cl As Color = device.background
            Dim enumCl As ConsoleColor = xConsole.ClosestConsoleColor(cl.R, cl.G, cl.B)

            Return enumCl
        End Get
        Set(value As ConsoleColor)
            device.background = Internal.FromConsoleColor(value.ToString)
        End Set
    End Property

    Public Property ForegroundColor As ConsoleColor Implements IConsole.ForegroundColor
        Get
            Dim cl As Color = device.foreground
            Dim enumCl As ConsoleColor = xConsole.ClosestConsoleColor(cl.R, cl.G, cl.B)

            Return enumCl
        End Get
        Set(value As ConsoleColor)
            device.foreground = Internal.FromConsoleColor(value.ToString)
        End Set
    End Property

    Public ReadOnly Property WindowWidth As Integer Implements IConsole.WindowWidth
        Get
            Using g = Graphics.FromHwnd(device.Handle)
                Dim size As SizeF = g.MeasureString("*", device.Font)
                Dim count As Integer = device.Size.Width / size.Width

                Return count
            End Using
        End Get
    End Property

    Public Event CancelKeyPress()

    Sub New(dev As ConsoleControl)
        device = dev
    End Sub

    Friend Sub TriggerCancelKeyPress()
        RaiseEvent CancelKeyPress()
    End Sub

    Public Sub SetConsoleForeColor(color As Color)
        device.ForeColor = color
    End Sub

    Public Sub SetConsoleBackColor(color As Color)
        device.BackColor = color
    End Sub

    Public Sub Clear() Implements IConsole.Clear
        device.Clear()
    End Sub

    Public Sub WriteLine() Implements IConsole.WriteLine
        Call Write(vbCr)
    End Sub

    Public Sub WriteLine(s As String, ParamArray args() As Object) Implements IConsole.WriteLine
        Call Write(String.Format(s, args) & vbCr)
    End Sub

    Public Function Read() As Integer Implements IConsole.Read
        SyncLock sharedStream
            If Not sharedStream.HaveChar Then
                Return -1
            Else
                Return AscW(sharedStream.GetChar)
            End If
        End SyncLock
    End Function

    Public Function ReadLine() As String Implements IConsole.ReadLine
        Return sharedStream.GetLine
    End Function

    Public Function ReadKey() As ConsoleKeyInfo Implements IConsole.ReadKey
        Dim keyChar As Char = sharedStream.GetChar
        Dim key As ConsoleKey
        Dim info As New ConsoleKeyInfo(keyChar, key, shift:=False, alt:=False, control:=False)

        Return info
    End Function

    Public Sub Write(str As String) Implements IConsole.Write
        Call device.Invoke(Sub() device.write(str))
    End Sub

    Public Sub WriteLine(str As String) Implements IConsole.WriteLine
        Call device.Invoke(Sub() device.write(str & vbCr))
    End Sub

    Public Sub WriteLine(img As Image)
        Dim bitmap As Bitmap = img
        Dim backup = Clipboard.GetDataObject

        Call WriteLine("")

        Clipboard.SetDataObject(bitmap)
        device.Invoke(Sub() device.Paste(DataFormats.GetFormat(DataFormats.Bitmap)))
        Clipboard.SetDataObject(backup)

        Call WriteLine("")
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
