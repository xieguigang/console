Imports System.IO
Imports System.Reflection
Imports System.Threading.Tasks
Imports Microsoft.Web.WebView2.Core
Imports Microsoft.Web.WebView2.WinForms

Namespace WebView2

    ''' <summary>
    ''' Owns WebView2 environment creation and serves the terminal's HTML/CSS/JS
    ''' from embedded resources.
    ''' </summary>
    ''' <remarks>
    ''' The renderer assets are compiled into this assembly rather than deployed as
    ''' loose files, so a consuming application only has to ship the DLL. They are
    ''' surfaced to the browser through a synthetic origin
    ''' (<see cref="VirtualHost"/>) whose requests are intercepted and answered
    ''' from the manifest resource stream. A real origin (rather than
    ''' <c>NavigateToString</c>) is required because the page loads several
    ''' scripts and a stylesheet by relative URL.
    ''' </remarks>
    Friend NotInheritable Class WebViewConsoleHost

        ''' <summary>
        ''' Synthetic origin for the embedded renderer assets. The <c>.invalid</c>
        ''' TLD is reserved by RFC 2606 and can never resolve on the network.
        ''' </summary>
        Public Const VirtualHost As String = "terminal.invalid"

        Public Const StartUrl As String = "https://" & VirtualHost & "/terminal.html"

        Private Const ResourceFolder As String = "WebView2.wwwroot."

        Private Shared ReadOnly AssetAssembly As Assembly = GetType(WebViewConsoleHost).Assembly

        ''' <summary>
        ''' Resolved once: the manifest prefix depends on the project's root
        ''' namespace and folder layout, so it is discovered rather than assumed.
        ''' </summary>
        Private Shared ReadOnly ResourcePrefix As New Lazy(Of String)(AddressOf ResolveResourcePrefix)

        Private ReadOnly m_view As Microsoft.Web.WebView2.WinForms.WebView2

        Private m_initialised As Boolean

        ''' <summary>
        ''' Raised once the browser is ready to receive messages.
        ''' </summary>
        Public Event Initialized()

        ''' <summary>
        ''' Raised when the browser could not be created, with a human-readable
        ''' explanation (typically a missing WebView2 Evergreen Runtime).
        ''' </summary>
        Public Event InitializationFailed(message As String)

        Public Sub New(view As Microsoft.Web.WebView2.WinForms.WebView2)
            m_view = view
        End Sub

        Public ReadOnly Property IsInitialized As Boolean
            Get
                Return m_initialised
            End Get
        End Property

        ''' <summary>
        ''' Locates the manifest name prefix under which the wwwroot assets were
        ''' embedded.
        ''' </summary>
        Private Shared Function ResolveResourcePrefix() As String
            Dim names As String() = AssetAssembly.GetManifestResourceNames()

            For Each name As String In names
                Dim marker As Integer = name.IndexOf(ResourceFolder, StringComparison.OrdinalIgnoreCase)

                If marker >= 0 Then
                    Return name.Substring(0, marker + ResourceFolder.Length)
                End If
            Next

            '  No assets embedded: GetAsset will return Nothing and the caller
            '  surfaces a diagnostic instead of silently rendering a blank page.
            Return Nothing
        End Function

        ''' <summary>
        ''' Reads an embedded asset by its file name, or <c>Nothing</c> when absent.
        ''' </summary>
        Public Shared Function GetAsset(fileName As String) As Byte()
            Dim prefix As String = ResourcePrefix.Value

            If prefix Is Nothing OrElse String.IsNullOrEmpty(fileName) Then
                Return Nothing
            End If

            '  Embedded resource names replace path separators with dots.
            Dim resourceName As String = prefix & fileName.Replace("/"c, "."c)

            Using stream As Stream = AssetAssembly.GetManifestResourceStream(resourceName)
                If stream Is Nothing Then
                    Return Nothing
                End If

                Using buffer As New MemoryStream()
                    stream.CopyTo(buffer)
                    Return buffer.ToArray()
                End Using
            End Using
        End Function

        Private Shared Function ContentTypeOf(fileName As String) As String
            Select Case Path.GetExtension(fileName).ToLowerInvariant()
                Case ".html", ".htm" : Return "text/html; charset=utf-8"
                Case ".css" : Return "text/css; charset=utf-8"
                Case ".js" : Return "application/javascript; charset=utf-8"
                Case ".json" : Return "application/json; charset=utf-8"
                Case ".svg" : Return "image/svg+xml"
                Case ".woff2" : Return "font/woff2"
                Case Else : Return "application/octet-stream"
            End Select
        End Function

        ''' <summary>
        ''' Creates the browser and navigates it to the embedded terminal page.
        ''' </summary>
        Public Async Function InitializeAsync(userDataFolder As String) As Task
            If m_initialised Then
                Return
            End If

            Try
                Dim environment As CoreWebView2Environment = Await CoreWebView2Environment.CreateAsync(Nothing, userDataFolder, Nothing)

                Await m_view.EnsureCoreWebView2Async(environment)

                Dim core As CoreWebView2 = m_view.CoreWebView2

                If core Is Nothing Then
                    RaiseEvent InitializationFailed("WebView2 could not be created.")
                    Return
                End If

                ConfigureSettings(core)

                AddHandler core.WebResourceRequested, AddressOf OnWebResourceRequested
                core.AddWebResourceRequestedFilter("https://" & VirtualHost & "/*", CoreWebView2WebResourceContext.All)

                If GetAsset("terminal.html") Is Nothing Then
                    RaiseEvent InitializationFailed(
                        "Terminal renderer assets are missing from " & AssetAssembly.GetName().Name & "." & System.Environment.NewLine &
                        "Ensure the WebView2\wwwroot files are included as EmbeddedResource.")
                    Return
                End If

                m_initialised = True

                core.Navigate(StartUrl)

                RaiseEvent Initialized()

            Catch ex As Exception
                RaiseEvent InitializationFailed(DescribeFailure(ex))
            End Try
        End Function

        Private Shared Sub ConfigureSettings(core As CoreWebView2)
            With core.Settings
                '  The terminal supplies its own right-click paste handling.
                .AreDefaultContextMenusEnabled = False
                .AreDevToolsEnabled = False
                .IsStatusBarEnabled = False
                '  Zooming would desynchronise the measured cell size from the
                '  row/column count reported to the pty.
                .IsZoomControlEnabled = False
                .IsSwipeNavigationEnabled = False
                .AreBrowserAcceleratorKeysEnabled = False
                .IsGeneralAutofillEnabled = False
                .IsPasswordAutosaveEnabled = False
                .IsBuiltInErrorPageEnabled = False
            End With
        End Sub

        Private Shared Function DescribeFailure(ex As Exception) As String
            If TypeOf ex Is WebView2RuntimeNotFoundException Then
                Return "The Microsoft Edge WebView2 Runtime is not installed." & Environment.NewLine &
                       "Install the Evergreen Runtime from " &
                       "https://developer.microsoft.com/microsoft-edge/webview2/ and restart the application."
            End If

            Return "Failed to initialise the WebView2 terminal:" & Environment.NewLine & ex.Message
        End Function

        Private Sub OnWebResourceRequested(sender As Object, e As CoreWebView2WebResourceRequestedEventArgs)
            Dim core As CoreWebView2 = m_view.CoreWebView2

            If core Is Nothing Then
                Return
            End If

            Dim requested As Uri = Nothing

            If Not Uri.TryCreate(e.Request.Uri, UriKind.Absolute, requested) Then
                Return
            End If

            Dim fileName As String = requested.AbsolutePath.TrimStart("/"c)

            If String.IsNullOrEmpty(fileName) Then
                fileName = "terminal.html"
            End If

            '  Reject anything trying to climb out of the asset folder.
            If fileName.Contains("..") Then
                e.Response = core.Environment.CreateWebResourceResponse(Nothing, 403, "Forbidden", String.Empty)
                Return
            End If

            Dim payload As Byte() = GetAsset(fileName)

            If payload Is Nothing Then
                e.Response = core.Environment.CreateWebResourceResponse(Nothing, 404, "Not Found", String.Empty)
                Return
            End If

            Dim headers As String =
                "Content-Type: " & ContentTypeOf(fileName) & vbCrLf &
                "Cache-Control: no-cache, no-store" & vbCrLf &
                "Access-Control-Allow-Origin: *"

            '  The stream is handed to WebView2, which disposes it after reading.
            e.Response = core.Environment.CreateWebResourceResponse(New MemoryStream(payload), 200, "OK", headers)
        End Sub

        ''' <summary>
        ''' Builds a per-user data folder so multiple hosting applications do not
        ''' contend over a single WebView2 profile.
        ''' </summary>
        Public Shared Function DefaultUserDataFolder() As String
            Dim root As String = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            Dim appName As String = AssetAssembly.GetName().Name

            Return Path.Combine(root, appName, "WebViewConsole")
        End Function

    End Class

End Namespace
