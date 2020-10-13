Imports System.Threading
Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.Language.UnixBash
Imports Console = Microsoft.VisualBasic.Windows.Forms.Console

Public Class DemoInterpreter

    Dim console As Console

    Public Sub Evaluate(lineInput As String)
        console.WriteLine("input any code")

        Dim getInput = console.ReadLine

        Call console.WriteLine("the code you've input is:")
        Call console.WriteLine(getInput)

        Call console.WriteLine("Press any key to continute...")
        Call console.ReadKey()

        Call console.WriteLine($"running [{lineInput}] job done!")
    End Sub

    Public Shared Sub Start(console As Console)
        console.SetPS1Pattern("\[.+\][$]\s")
        console.Write("Microsoft Windows [Version 10.0.19041.508]
(c) 2020 Microsoft Corporation. All rights reserved.

")

        Dim Shell As New Shell(PS1.Fedora12, AddressOf New DemoInterpreter With {.console = console}.Evaluate, console)
        Call New Thread(AddressOf Shell.Run).Start()
    End Sub
End Class
