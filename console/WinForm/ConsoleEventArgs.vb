''' <summary>
''' The ConsoleEventArgs are arguments for a console event.
''' </summary>
Public Class ConsoleEventArgs : Inherits EventArgs

    ''' <summary>
    ''' Gets the content.
    ''' </summary>
    Public ReadOnly Property Content As String

    ''' <summary>
    ''' Initializes a new instance of the <see cref="ConsoleEventArgs"/> class.
    ''' </summary>
    Public Sub New()
    End Sub

    ''' <summary>
    ''' Initializes a new instance of the <see cref="ConsoleEventArgs"/> class.
    ''' </summary>
    ''' <param name="content">The content.</param>
    Public Sub New(content As String)
        '  Set the content.
        Me.Content = content
    End Sub

    Public Overrides Function ToString() As String
        Return Content
    End Function
End Class