Imports System.IO
Imports System.Threading

Public Class Console : Implements IDisposable

    ReadOnly device As ConsoleControl
    ReadOnly sharedStream As New MemoryStream
    ReadOnly reader As StreamReader

    Sub New(dev As ConsoleControl)
        device = dev
        reader = New StreamReader(sharedStream)
    End Sub

    Friend Function OpenInput() As StreamWriter
        Return New StreamWriter(sharedStream)
    End Function

    Public Function ReadLine() As String
        Return reader.ReadLine
    End Function

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
