﻿Imports System.Threading
Imports Microsoft.VisualBasic.Text

Public Class PipelineStream

    ''' <summary>
    ''' <see cref="Console.ReadKey"/>
    ''' </summary>
    Dim m_buffer As New Queue(Of Char)
    ''' <summary>
    ''' <see cref="Console.ReadLine"/>
    ''' </summary>
    Dim m_commit As New Queue(Of String)

    Public Sub ClearCharBuffer()
        SyncLock m_buffer
            Call m_buffer.Clear()
        End SyncLock
    End Sub

    Public Sub Push(c As Char)
        SyncLock m_buffer
            m_buffer.Enqueue(c)
        End SyncLock
    End Sub

    Public Sub Commit(line As String)
        SyncLock m_commit
            m_commit.Enqueue(line)
            m_buffer.Clear()
            m_buffer.Enqueue(ASCII.CR)
        End SyncLock
    End Sub

    Public Function HaveChar() As Boolean
        Return m_buffer.Count > 0
    End Function

    Public Function GetChar() As Char
RE0:    Do While m_buffer.Count = 0
            ' simulate I/O block
            Thread.Sleep(10)
        Loop

        SyncLock m_buffer
            If m_buffer.Count = 0 Then
                GoTo RE0
            End If

            Return m_buffer.Dequeue
        End SyncLock
    End Function

    Public Function GetLine() As String
RE0:    Do While m_commit.Count = 0
            ' simulate I/O block
            Thread.Sleep(10)
        Loop

        SyncLock m_commit
            If m_commit.Count = 0 Then
                GoTo RE0
            Else
                m_buffer.Clear()
            End If

            Return m_commit.Dequeue
        End SyncLock
    End Function

End Class
