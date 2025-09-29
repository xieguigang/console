''' <summary>
''' A KeyMapping defines how a key combination should
''' be mapped to a SendKeys message.
''' </summary>
Public Class KeyMapping

    ''' <summary>
    ''' Gets or sets a value indicating whether this instance is control pressed.
    ''' </summary>
    ''' <value>
    ''' 	<c>true</c> if this instance is control pressed; otherwise, <c>false</c>.
    ''' </value>
    Public Property IsControlPressed As Boolean

    ''' <summary>
    ''' Gets or sets a value indicating whether alt is pressed.
    ''' </summary>
    ''' <value>
    ''' 	<c>true</c> if this instance is alt pressed; otherwise, <c>false</c>.
    ''' </value>
    Public Property IsAltPressed As Boolean

    ''' <summary>
    ''' Gets or sets a value indicating whether this instance is shift pressed.
    ''' </summary>
    ''' <value>
    ''' 	<c>true</c> if this instance is shift pressed; otherwise, <c>false</c>.
    ''' </value>
    Public Property IsShiftPressed As Boolean

    ''' <summary>
    ''' Gets or sets the key code.
    ''' </summary>
    ''' <value>
    ''' The key code.
    ''' </value>
    Public Property KeyCode As Keys

    ''' <summary>
    ''' Gets or sets the send keys mapping.
    ''' </summary>
    ''' <value>
    ''' The send keys mapping.
    ''' </value>
    Public Property SendKeysMapping As String

    ''' <summary>
    ''' Gets or sets the stream mapping.
    ''' </summary>
    ''' <value>
    ''' The stream mapping.
    ''' </value>
    Public Property StreamMapping As String

    ''' <summary>
    ''' Initializes a new instance of the <seecref="KeyMapping"/> class.
    ''' </summary>
    Public Sub New()
    End Sub

    ''' <summary>
    ''' Initializes a new instance of the <seecref="KeyMapping"/> class.
    ''' </summary>
    ''' <paramname="control">if set to <c>true</c> [control].</param>
    ''' <paramname="alt">if set to <c>true</c> [alt].</param>
    ''' <paramname="shift">if set to <c>true</c> [shift].</param>
    ''' <paramname="keyCode">The key code.</param>
    ''' <paramname="sendKeysMapping">The send keys mapping.</param>
    ''' <paramname="streamMapping">The stream mapping.</param>
    Public Sub New(control As Boolean, alt As Boolean, shift As Boolean, keyCode As Keys, sendKeysMapping As String, streamMapping As String)
        '  Set the member variables.
        IsControlPressed = control
        IsAltPressed = alt
        IsShiftPressed = shift
        Me.KeyCode = keyCode
        Me.SendKeysMapping = sendKeysMapping
        Me.StreamMapping = streamMapping
    End Sub
End Class
