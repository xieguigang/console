Imports System.Runtime.CompilerServices
Imports Microsoft.VisualBasic.Imaging

Module Internal

    <Extension>
    Friend Function FromConsoleColor(exp As String) As Color
        ' 2019-03-14 有些console的颜色是不存在的,所以解析会得到黑色
        Dim color As Color = exp.TranslateColor(False)

        If Not color.IsEmpty Then
            Return color
        Else
            ' 使用相近的颜色进行替代
            If InStr(exp, "Dark") > 0 Then
                exp = exp.Replace("Dark", "")
                color = exp.TranslateColor.Darken
            ElseIf InStr(exp, "Light") > 0 Then
                exp = exp.Replace("Light", "")
                color = exp.TranslateColor.Lighten
            Else
                Throw New NotImplementedException(exp)
            End If

            Return color
        End If
    End Function
End Module
