Imports System.Text
Imports System.Threading
Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.Windows.Forms.Win32
Imports Renci.SshNet.Common
Imports SSH = Renci.SshNet

Public Class LocalShellInterface : Inherits AbstractProcessInterface

    Public Sub New()
        MyBase.New(Nothing)
        ansi = True
    End Sub

    Public Overrides Sub WriteInput(input As String)
        Call RaiseOutputEvent(New TextSpan(Environment.UserName & "@" & Environment.MachineName, AnsiColor.Green) & AnsiEscapeCodes.Reset & " ")
        '  Call RaiseOutputEvent("$ " & AnsiEscapeCodes.GetMoveCursorUp(1))
    End Sub

    Public Overrides Sub WriteRaw(input As String)

    End Sub
End Class