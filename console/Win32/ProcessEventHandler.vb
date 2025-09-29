Namespace Win32

    ''' <summary>
    ''' A ProcessEventHandler is a delegate for process input/output events.
    ''' </summary>
    ''' <paramname="sender">The sender.</param>
    ''' <paramname="args">The <seecref="ProcessEventArgs"/> instance containing the event data.</param>
    Public Delegate Sub ProcessEventHandler(sender As Object, args As ProcessEventArgs)

End Namespace