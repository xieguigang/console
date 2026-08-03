Imports System.Text
Imports System.Text.Encodings.Web
Imports System.Text.Json
Imports System.Text.Json.Serialization

Namespace WebView2

    ''' <summary>
    ''' Message types the host sends down to the renderer.
    ''' </summary>
    Friend Enum OutboundMessageKind
        Output
        Style
        Config
        Clear
        Scrollback
        Focus
        SetLine
    End Enum

    ''' <summary>
    ''' Message types the renderer sends up to the host.
    ''' </summary>
    Friend Enum InboundMessageKind
        Unknown
        Ready
        Input
        Raw
        Resize
        Bell
    End Enum

    ''' <summary>
    ''' A message received from the terminal renderer.
    ''' </summary>
    Friend Class InboundMessage

        Public Property Kind As InboundMessageKind
        Public Property Data As String
        Public Property Columns As Integer
        Public Property Rows As Integer

        ''' <summary>
        ''' The renderer's current (uncommitted) input line. Only populated on
        ''' <see cref="InboundMessageKind.Raw"/> messages, so that back-ends which
        ''' implement tab completion can see what the user has typed so far.
        ''' </summary>
        Public Property Line As String

        ''' <summary>
        ''' Caret offset within <see cref="Line"/> when the raw key was pressed.
        ''' </summary>
        Public Property CursorPosition As Integer

    End Class

    ''' <summary>
    ''' A key chord mapping serialised down to the renderer, which performs the
    ''' actual key translation because the WebView owns the input focus.
    ''' </summary>
    Friend Class KeyMappingPayload

        <JsonPropertyName("ctrl")>
        Public Property Ctrl As Boolean

        <JsonPropertyName("alt")>
        Public Property Alt As Boolean

        <JsonPropertyName("shift")>
        Public Property Shift As Boolean

        ''' <summary>
        ''' The DOM <c>KeyboardEvent.key</c> value this mapping matches.
        ''' </summary>
        <JsonPropertyName("key")>
        Public Property Key As String

        ''' <summary>
        ''' The exact byte string to deliver to the process.
        ''' </summary>
        <JsonPropertyName("data")>
        Public Property Data As String

    End Class

    ''' <summary>
    ''' Serialisation helpers for the VB &lt;-&gt; JS message channel.
    ''' </summary>
    ''' <remarks>
    ''' Both sides speak single-level JSON objects routed by a <c>type</c> field.
    ''' Property names are lower camel case to match the JavaScript side exactly.
    ''' </remarks>
    Friend NotInheritable Class TerminalMessage

        Private Sub New()
        End Sub

        ''' <summary>
        ''' Relaxed encoding: the payload never reaches an HTML context (it is
        ''' handed to the renderer through the typed message channel), so escaping
        ''' every non-ASCII character would only inflate the traffic.
        ''' </summary>
        Private Shared ReadOnly WriterOptions As New JsonSerializerOptions With {
            .Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            .WriteIndented = False
        }

        ''' <summary>
        ''' Builds an <c>output</c> message carrying a raw (possibly ANSI-bearing)
        ''' chunk of process output.
        ''' </summary>
        Public Shared Function Output(data As String) As String
            Dim builder As New StringBuilder(data.Length + 32)

            builder.Append("{""type"":""output"",""data"":")
            AppendJsonString(builder, data)
            builder.Append("}"c)

            Return builder.ToString()
        End Function

        ''' <summary>
        ''' Builds a <c>style</c> message describing the font and default colours.
        ''' </summary>
        Public Shared Function Style(fontFamily As String,
                                     fontSizePixels As Double,
                                     foreColor As Color,
                                     backColor As Color) As String

            Return JsonSerializer.Serialize(New Dictionary(Of String, Object) From {
                {"type", "style"},
                {"fontFamily", QuoteFontFamily(fontFamily)},
                {"fontSize", fontSizePixels.ToString("0.##", Globalization.CultureInfo.InvariantCulture) & "px"},
                {"foreColor", ToCssColor(foreColor)},
                {"backColor", ToCssColor(backColor)}
            }, WriterOptions)
        End Function

        ''' <summary>
        ''' Builds a <c>config</c> message describing the input behaviour.
        ''' </summary>
        Public Shared Function Config(inputEnabled As Boolean,
                                      isReadOnly As Boolean,
                                      sendKeysToProcess As Boolean,
                                      mappings As IEnumerable(Of KeyMappingPayload)) As String

            Return JsonSerializer.Serialize(New Dictionary(Of String, Object) From {
                {"type", "config"},
                {"inputEnabled", inputEnabled},
                {"readOnly", isReadOnly},
                {"sendKeysToProcess", sendKeysToProcess},
                {"keyMappings", If(mappings, Enumerable.Empty(Of KeyMappingPayload)()).ToArray()}
            }, WriterOptions)
        End Function

        Public Shared Function Clear() As String
            Return "{""type"":""clear""}"
        End Function

        Public Shared Function Focus() As String
            Return "{""type"":""focus""}"
        End Function

        Public Shared Function Scrollback(lines As Integer) As String
            Return "{""type"":""scrollback"",""lines"":" & lines.ToString(Globalization.CultureInfo.InvariantCulture) & "}"
        End Function

        ''' <summary>
        ''' Builds a <c>setLine</c> message asking the renderer to replace the
        ''' current editable input line with <paramref name="text"/> and park the
        ''' caret at its end. Used by back-ends that rewrite the line themselves,
        ''' such as tab completion, so the on-screen text and the renderer's line
        ''' buffer cannot drift apart.
        ''' </summary>
        Public Shared Function SetLine(text As String) As String
            Dim builder As New StringBuilder(If(text, String.Empty).Length + 32)

            builder.Append("{""type"":""setLine"",""data"":")
            AppendJsonString(builder, If(text, String.Empty))
            builder.Append("}"c)

            Return builder.ToString()
        End Function

        ''' <summary>
        ''' Parses a message posted by the renderer. Returns <c>Nothing</c> when the
        ''' payload is not understood, so callers can ignore it safely.
        ''' </summary>
        Public Shared Function Parse(json As String) As InboundMessage
            If String.IsNullOrEmpty(json) Then
                Return Nothing
            End If

            Try
                Using document As JsonDocument = JsonDocument.Parse(json)
                    Dim root As JsonElement = document.RootElement

                    If root.ValueKind <> JsonValueKind.Object Then
                        Return Nothing
                    End If

                    Dim typeElement As JsonElement

                    If Not root.TryGetProperty("type", typeElement) Then
                        Return Nothing
                    End If

                    Dim message As New InboundMessage With {
                        .Kind = ParseKind(typeElement.GetString())
                    }

                    Dim dataElement As JsonElement

                    If root.TryGetProperty("data", dataElement) AndAlso dataElement.ValueKind = JsonValueKind.String Then
                        message.Data = dataElement.GetString()
                    End If

                    Dim colsElement As JsonElement

                    If root.TryGetProperty("cols", colsElement) AndAlso colsElement.ValueKind = JsonValueKind.Number Then
                        message.Columns = colsElement.GetInt32()
                    End If

                    Dim rowsElement As JsonElement

                    If root.TryGetProperty("rows", rowsElement) AndAlso rowsElement.ValueKind = JsonValueKind.Number Then
                        message.Rows = rowsElement.GetInt32()
                    End If

                    Dim lineElement As JsonElement

                    If root.TryGetProperty("line", lineElement) AndAlso lineElement.ValueKind = JsonValueKind.String Then
                        message.Line = lineElement.GetString()
                    End If

                    Dim cursorElement As JsonElement

                    If root.TryGetProperty("cursor", cursorElement) AndAlso cursorElement.ValueKind = JsonValueKind.Number Then
                        message.CursorPosition = cursorElement.GetInt32()
                    End If

                    Return message
                End Using
            Catch
                '  A malformed payload must never take the control down.
                Return Nothing
            End Try
        End Function

        Private Shared Function ParseKind(value As String) As InboundMessageKind
            Select Case value
                Case "ready" : Return InboundMessageKind.Ready
                Case "input" : Return InboundMessageKind.Input
                Case "raw" : Return InboundMessageKind.Raw
                Case "resize" : Return InboundMessageKind.Resize
                Case "bell" : Return InboundMessageKind.Bell
                Case Else : Return InboundMessageKind.Unknown
            End Select
        End Function

        ''' <summary>
        ''' Emits a JSON string literal.
        ''' </summary>
        ''' <remarks>
        ''' Hand-rolled rather than routed through <see cref="JsonSerializer"/>
        ''' because output messages are the hot path: this avoids allocating an
        ''' intermediate dictionary and a second buffer for every chunk. Control
        ''' characters below 0x20 are escaped as <c>\uXXXX</c>, which matters here
        ''' since terminal output is full of ESC (0x1B) bytes.
        ''' </remarks>
        Private Shared Sub AppendJsonString(builder As StringBuilder, value As String)
            builder.Append(""""c)

            If Not String.IsNullOrEmpty(value) Then
                For Each c As Char In value
                    Select Case c
                        Case """"c
                            builder.Append("\""")
                        Case "\"c
                            builder.Append("\\")
                        Case ChrW(8)
                            builder.Append("\b")
                        Case ChrW(12)
                            builder.Append("\f")
                        Case ChrW(10)
                            builder.Append("\n")
                        Case ChrW(13)
                            builder.Append("\r")
                        Case ChrW(9)
                            builder.Append("\t")
                        Case Else
                            If AscW(c) < &H20 Then
                                builder.Append("\u")
                                builder.Append(AscW(c).ToString("x4"))
                            Else
                                builder.Append(c)
                            End If
                    End Select
                Next
            End If

            builder.Append(""""c)
        End Sub

        ''' <summary>
        ''' Converts a <see cref="Color"/> to the <c>#RRGGBB</c> form CSS expects.
        ''' </summary>
        Public Shared Function ToCssColor(value As Color) As String
            Return "#" & value.R.ToString("x2") & value.G.ToString("x2") & value.B.ToString("x2")
        End Function

        ''' <summary>
        ''' Builds a CSS font-family list from a WinForms font name, always keeping
        ''' generic monospace fallbacks so the grid never degrades to a
        ''' proportional face.
        ''' </summary>
        Public Shared Function QuoteFontFamily(name As String) As String
            Dim fallback As String = "Consolas, ""Cascadia Mono"", ""Courier New"", monospace"

            If String.IsNullOrWhiteSpace(name) Then
                Return fallback
            End If

            Return """" & name.Replace("""", "") & """, " & fallback
        End Function

    End Class

End Namespace
