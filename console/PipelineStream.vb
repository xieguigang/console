Imports System.IO
Imports System.Threading

Public Class PipelineStream

    Dim buffer As Queue(Of Char)

    Public Function GetChar() As Char
        Do While buffer.Count = 0
            ' simulate I/O block
            Thread.Sleep(10)
        Loop

        SyncLock buffer
            Return buffer.Dequeue
        End SyncLock
    End Function

    Public Function GetLine() As String
        Dim buffer As New List(Of Char)


    End Function

End Class
