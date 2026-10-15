''' <summary>
''' The ConsoleEventArgs are arguments for a console event.
''' </summary>
Public Class ConsoleEventArgs : Inherits EventArgs

    ''' <summary>
    ''' Gets the content.
    ''' </summary>
    Public ReadOnly Property Content As String
    ''' <summary>
    ''' stdout/stderr
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Type As String

    ''' <summary>
    ''' Initializes a new instance of the <see cref="ConsoleEventArgs"/> class.
    ''' </summary>
    Public Sub New()
    End Sub

    ''' <summary>
    ''' Initializes a new instance of the <see cref="ConsoleEventArgs"/> class.
    ''' </summary>
    ''' <param name="content">The content.</param>
    ''' <remarks>
    ''' type of the output content stream could be: stdout / stderr
    ''' </remarks>
    Public Sub New(content As String, Optional type As String = "stdout")
        '  Set the content.
        Me.Type = type
        Me.Content = content
    End Sub

    Public Overrides Function ToString() As String
        Return Content
    End Function
End Class