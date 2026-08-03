Namespace Win32

    ''' <summary>
    ''' The ProcessEventArgs are arguments for a console event.
    ''' </summary>
    Public Class ProcessEventArgs : Inherits EventArgs

        ''' <summary>
        ''' Gets the content.
        ''' </summary>
        Public ReadOnly Property Content As String

        ''' <summary>
        ''' Gets or sets the code.
        ''' </summary>
        ''' <value>
        ''' The code.
        ''' </value>
        Public ReadOnly Property Code As Integer?

        Public ReadOnly Property Ansi As Boolean = False

        ''' <summary>
        ''' Initializes a new instance of the <seecref="ProcessEventArgs"/> class.
        ''' </summary>
        Public Sub New()
        End Sub

        Public Sub New(content As String)
            Me.Content = content
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <seecref="ProcessEventArgs"/> class.
        ''' </summary>
        ''' <param name="content">The content.</param>
        ''' <param name="ansi">The content contains ANSI escape codes.</param>
        Public Sub New(content As String, ansi As Boolean)
            '  Set the content and code.
            Me.Content = content
            Me.Ansi = ansi
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <seecref="ProcessEventArgs"/> class.
        ''' </summary>
        ''' <param name="code">The code.</param>
        Public Sub New(code As Integer)
            '  Set the content and code.
            Me.Code = code
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <seecref="ProcessEventArgs"/> class.
        ''' </summary>
        ''' <param name="content">The content.</param>
        ''' <param name="code">The code.</param>
        Public Sub New(content As String, code As Integer)
            '  Set the content and code.
            Me.Content = content
            Me.Code = code
        End Sub
    End Class
End Namespace
