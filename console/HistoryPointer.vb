Imports Microsoft.VisualBasic.Language

Public Class HistoryPointer

    Dim history As New List(Of String)
    Dim i As Integer = Scan0

    Public Function GetPrevious(ByRef endOfHistory As Boolean) As String
        endOfHistory = i = 0

        If endOfHistory Then
            Return Nothing
        Else
            i -= 1
            Return history(i)
        End If
    End Function

    Public Function GetNext(ByRef endOfHistory As Boolean) As String
        endOfHistory = i = history.Count - 1

        If endOfHistory Then
            Return Nothing
        Else
            i += 1
            Return history(i)
        End If
    End Function

    Public Function GetByKey(key As Keys, ByRef endOfHistory As Boolean) As String
        If key = Keys.Up Then
            Return GetPrevious(endOfHistory)
        ElseIf key = Keys.Down Then
            Return GetNext(endOfHistory)
        Else
            Throw New ArgumentException(key.ToString)
        End If
    End Function

    Public Function Push(line As String) As Integer
        history.Add(line)
        i = history.Count - 1
        Return i + 1
    End Function
End Class
