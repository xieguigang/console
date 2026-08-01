Imports System.Windows.Forms

Namespace SShClient

    ''' <summary>
    ''' Application entry point. Launches the demo <see cref="MainForm"/> so the
    ''' <see cref="SshWinFormConsole"/> control can be exercised directly.
    ''' </summary>
    Public Class Program

        <STAThread>
        Public Shared Sub Main()
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)
            Application.Run(New MainForm())
        End Sub
    End Class
End Namespace
