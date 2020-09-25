Imports System.IO
Imports System.Threading

Public Class Console : Implements IDisposable

    Friend ReadOnly device As ConsoleControl
    Friend ReadOnly sharedStream As New PipelineStream

    Sub New(dev As ConsoleControl)
        device = dev
    End Sub

    Public Function ReadLine() As String
        Return sharedStream.GetLine
    End Function

    Public Function ReadKey() As Char
        Return sharedStream.GetChar
    End Function

    Public Sub Write(c As Char)
        Call device.Invoke(Sub() device.writeChar(c))
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
