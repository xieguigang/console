Imports Microsoft.VisualBasic.Windows.Forms.Win32

''' <summary>
''' 终端控件公共契约。
''' </summary>
''' <remarks>
''' 由 RichTextBox 实现（<see cref="ConsoleControl"/>）与 WebView2 实现
''' （<see cref="WebViewConsole"/>）共同实现，使调用方可以在两种渲染后端之间
''' 无缝互换。
''' <para>
''' 注意：<c>InternalRichTextBox</c> 属于 RichTextBox 实现的专有细节，
''' 不进入本接口。
''' </para>
''' </remarks>
Public Interface IConsoleControl

    ''' <summary>
    ''' 控件是否处于只读状态（不接受用户键入）。
    ''' </summary>
    Property [ReadOnly] As Boolean

    ''' <summary>
    ''' 是否允许用户键入输入。
    ''' </summary>
    Property IsInputEnabled As Boolean

    ''' <summary>
    ''' 是否把 Ctrl-C、Tab 之类的特殊按键转发给后端进程。
    ''' </summary>
    Property SendKeyboardCommandsToProcess As Boolean

    ''' <summary>
    ''' 是否显示诊断信息（如进程启动/退出提示）。
    ''' </summary>
    Property ShowDiagnostics As Boolean

    ''' <summary>
    ''' 后端会话/进程是否正在运行。
    ''' </summary>
    ReadOnly Property IsProcessRunning As Boolean

    ''' <summary>
    ''' 当前绑定的后端进程接口。
    ''' </summary>
    ReadOnly Property ProcessInterface As AbstractProcessInterface

    ''' <summary>
    ''' 按键映射表。
    ''' </summary>
    ReadOnly Property KeyMappings As List(Of KeyMapping)

    ''' <summary>
    ''' 由渲染层实测的终端网格列数，供后端设置伪终端窗口大小。
    ''' </summary>
    ReadOnly Property TerminalColumns As Integer

    ''' <summary>
    ''' 由渲染层实测的终端网格行数，供后端设置伪终端窗口大小。
    ''' </summary>
    ReadOnly Property TerminalRows As Integer

    ''' <summary>
    ''' 绑定后端进程接口。
    ''' </summary>
    Sub SetConsoleCore([interface] As AbstractProcessInterface)

    ''' <summary>
    ''' 取得当前绑定的后端进程接口。
    ''' </summary>
    Function GetInterface() As AbstractProcessInterface

    ''' <summary>
    ''' 以指定颜色写入一段纯文本输出。
    ''' </summary>
    Sub WriteOutput(output As String, color As Color)

    ''' <summary>
    ''' 写入一段可能包含 ANSI escape sequence 的输出并渲染。
    ''' </summary>
    Sub WriteAnsiEscape(ansiText As String)

    ''' <summary>
    ''' 把一行输入发送给后端（会附加换行），可选是否回显。
    ''' </summary>
    Sub WriteInput(input As String, color As Color, echo As Boolean)

    ''' <summary>
    ''' 把原始字节序列发送给后端，不附加任何行终止符。
    ''' 用于传递 Ctrl+C（ETX）之类必须原样抵达的控制信号。
    ''' </summary>
    Sub WriteRaw(raw As String)

    ''' <summary>
    ''' 清空终端显示内容。
    ''' </summary>
    Sub ClearOutput()

    ''' <summary>
    ''' 启动后端会话（连接参数由后端自行配置）。
    ''' </summary>
    Sub StartProcess()

    ''' <summary>
    ''' 启动指定的本地进程。
    ''' </summary>
    Sub StartProcess(fileName As String, arguments As String)

    ''' <summary>
    ''' 停止后端会话/进程。
    ''' </summary>
    Sub StopProcess()

    ''' <summary>
    ''' 产生控制台输出时触发。
    ''' </summary>
    Event OnConsoleOutput(sender As Object, args As ConsoleEventArgs)

    ''' <summary>
    ''' 产生控制台输入时触发。
    ''' </summary>
    Event OnConsoleInput(sender As Object, args As ConsoleEventArgs)

End Interface
