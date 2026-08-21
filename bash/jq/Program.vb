Imports System.IO
Imports Microsoft.VisualBasic.CommandLine
Imports Microsoft.VisualBasic.MIME.application.json
Imports Microsoft.VisualBasic.MIME.application.json.Javascript

Module Program
    Sub Main(args As String())
        Dim opts As JqOpts = CommandLine.BuildFromArguments(args, NoSubCommand:=True).CreateOpts(Of JqOpts)
        Dim json_str As String

        ' json string comes from the standard input if the -i input argument is missing
        If opts.input.StringEmpty() Then
            json_str = New StreamReader(Console.OpenStandardInput).ReadToEnd
        Else
            ' or read json string from file
            json_str = opts.input.ReadAllText
        End If

        Dim json As JsonElement = JsonElement.ParseJSON(json_str)

        ' processing json document content
        json = JsonQuery(json, opts)
        ' pretty print
        json_str = json.BuildJsonString(opts:=New JSONSerializerOptions With {.indent = True})

        Call Console.WriteLine(json_str)
    End Sub

    Private Function JsonQuery(doc As JsonElement, opts As JqOpts) As JsonElement
        Return doc
    End Function
End Module
