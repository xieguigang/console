Namespace Win32

    ''' <summary>
    ''' A ProcessEventHandler is a delegate for process input/output events.
    ''' </summary>
    ''' <param name="sender">The sender.</param>
    ''' <param name="args">The <see cref="ProcessEventArgs"/> instance containing the event data.</param>
    Public Delegate Sub ProcessEventHandler(sender As Object, args As ProcessEventArgs)

End Namespace