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

        ''' <summary>
        ''' Initializes a new instance of the <seecref="ProcessEventArgs"/> class.
        ''' </summary>
        Public Sub New()
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <seecref="ProcessEventArgs"/> class.
        ''' </summary>
        ''' <paramname="content">The content.</param>
        Public Sub New(content As String)
            '  Set the content and code.
            Me.Content = content
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <seecref="ProcessEventArgs"/> class.
        ''' </summary>
        ''' <paramname="code">The code.</param>
        Public Sub New(code As Integer)
            '  Set the content and code.
            Me.Code = code
        End Sub

        ''' <summary>
        ''' Initializes a new instance of the <seecref="ProcessEventArgs"/> class.
        ''' </summary>
        ''' <paramname="content">The content.</param>
        ''' <paramname="code">The code.</param>
        Public Sub New(content As String, code As Integer)
            '  Set the content and code.
            Me.Content = content
            Me.Code = code
        End Sub
    End Class
End Namespace
