<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()> _
Partial Class WebViewConsole
    Inherits System.Windows.Forms.UserControl

    'UserControl overrides dispose to clean up the component list.
    <System.Diagnostics.DebuggerNonUserCode()> _
    Protected Overrides Sub Dispose(ByVal disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    'Required by the Windows Form Designer
    Private components As System.ComponentModel.IContainer

    'NOTE: The following procedure is required by the Windows Form Designer
    'It can be modified using the Windows Form Designer.  
    'Do not modify it using the code editor.
    <System.Diagnostics.DebuggerStepThrough()> _
    Private Sub InitializeComponent()
        WebView21 = New Web.WebView2.WinForms.WebView2()
        CType(WebView21, System.ComponentModel.ISupportInitialize).BeginInit()
        SuspendLayout()
        ' 
        ' WebView21
        ' 
        WebView21.AllowExternalDrop = False
        WebView21.CreationProperties = Nothing
        '  Black matches the terminal background, so the control does not flash
        '  white while the browser is still starting up.
        WebView21.DefaultBackgroundColor = Color.Black
        WebView21.Dock = DockStyle.Fill
        WebView21.Location = New Point(0, 0)
        WebView21.Name = "WebView21"
        WebView21.Size = New Size(597, 372)
        WebView21.TabIndex = 0
        WebView21.ZoomFactor = 1R
        ' 
        ' WebViewConsole
        ' 
        AutoScaleDimensions = New SizeF(7F, 15F)
        AutoScaleMode = AutoScaleMode.Font
        Controls.Add(WebView21)
        Name = "WebViewConsole"
        Size = New Size(597, 372)
        CType(WebView21, System.ComponentModel.ISupportInitialize).EndInit()
        ResumeLayout(False)
    End Sub

    Friend WithEvents WebView21 As Microsoft.Web.WebView2.WinForms.WebView2

End Class
