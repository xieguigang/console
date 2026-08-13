Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.Windows.Forms.Win32

''' <summary>
''' A built-in local shell backend that simulates a bash-like environment without
''' launching an external process. It intercepts the <c>ssh</c> command and raises
''' <see cref="SshConnectRequested"/> so the host control can switch to a real SSH
''' session. All other commands (file / text, system-info, shell builtins and
''' utilities) are executed directly using .NET System.IO, maintaining session
''' state (environment variables, aliases and command history).
''' </summary>
Public Class LocalShellInterface : Inherits AbstractProcessInterface : Implements IEditableInputLine

    Private _running As Boolean = False
    Private _cwd As String = Environment.CurrentDirectory

    ''' <summary>Session environment variables (HOME, USER, PATH, PS1, LANG, ...).</summary>
    Private _env As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Command aliases, expanded before dispatch.</summary>
    Private _aliases As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    ''' <summary>Command history, newest entries at the end.</summary>
    Private _history As New List(Of String)()

    ''' <summary>Fired when the user types <c>ssh [-p port] user@host</c>.</summary>
    Public Event SshConnectRequested(options As SshConnectionOptions)

    Public Sub New()
        MyBase.New(Nothing)
        ansi = True
    End Sub

#Region "Abstract overrides"

    Public Overrides ReadOnly Property IsProcessRunning As Boolean
        Get
            Return _running
        End Get
    End Property

    Public Overrides Sub StartProcess()
        _running = True
        _cwd = Environment.CurrentDirectory
        InitSessionState()
        ShowPrompt()
    End Sub

    ''' <summary>
    ''' Seeds the session environment, aliases and history so the simulated
    ''' shell starts from a sane, bash-like baseline.
    ''' </summary>
    Private Sub InitSessionState()
        _env.Clear()
        _env("HOME") = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        _env("USER") = Environment.UserName
        _env("LOGNAME") = Environment.UserName
        _env("HOSTNAME") = Environment.MachineName
        _env("PATH") = Environment.GetEnvironmentVariable("PATH")
        _env("LANG") = "en_US.UTF-8"
        _env("SHELL") = "/bin/bash"
        _env("PS1") = "\u@\h:\w\$ "
        _env("PWD") = _cwd
        _env("OLDPWD") = _cwd
        _env("TERM") = "xterm-256color"

        _aliases.Clear()
        _aliases("ll") = "ls -l"
        _aliases("la") = "ls -a"
        _aliases("l") = "ls -la"
        _aliases("cls") = "clear"
        _aliases("grep") = "grep --color=auto"

        _history.Clear()
    End Sub

    Public Overrides Sub StopProcess()
        _running = False
    End Sub

    ''' <summary>
    ''' Called by the console control when the user presses Enter on a completed
    ''' line of input. Parses and executes the command locally.
    ''' </summary>
    Public Overrides Sub WriteInput(input As String)
        If Not _running Then Return

        '  Start output on a new line (the cursor is at the end of the input line).
        ' RaiseOutputEvent(vbLf)

        Dim line As String = If(input, "").Trim()

        If String.IsNullOrEmpty(line) Then
            ShowPrompt()
            Return
        End If

        '  Record the line in history (de-duplicated against the previous entry).
        If _history.Count = 0 OrElse _history(_history.Count - 1) <> line Then
            _history.Add(line)
        End If

        '  Reset the working copy; alias expansion may rewrite it.
        Dim effectiveLine As String = ExpandAlias(line)

        Dim tokens As String() = ParseTokens(effectiveLine)
        If tokens.Length = 0 Then
            ShowPrompt()
            Return
        End If

        Dim cmd As String = tokens(0).ToLowerInvariant()

        Try
            Select Case cmd
                Case "ssh"
                    HandleSsh(tokens)
                Case "pwd"
                    CmdPwd()
                Case "ls"
                    CmdLs(tokens)
                Case "cd"
                    CmdCd(tokens)
                Case "cat"
                    CmdCat(tokens)
                Case "echo"
                    CmdEcho(tokens)
                Case "mkdir"
                    CmdMkdir(tokens)
                Case "rm"
                    CmdRm(tokens)
                Case "clear"
                    CmdClear()
                Case "whoami"
                    CmdWhoami()
                Case "help"
                    CmdHelp()
                Case "exit"
                    CmdExit()

                '  ---- File / text commands ----
                Case "cp"
                    CmdCp(tokens)
                Case "mv"
                    CmdMv(tokens)
                Case "touch"
                    CmdTouch(tokens)
                Case "head"
                    CmdHead(tokens)
                Case "tail"
                    CmdTail(tokens)
                Case "grep"
                    CmdGrep(tokens)
                Case "wc"
                    CmdWc(tokens)
                Case "find"
                    CmdFind(tokens)
                Case "file"
                    CmdFile(tokens)
                Case "ln"
                    CmdLn(tokens)
                Case "sort"
                    CmdSort(tokens)
                Case "uniq"
                    CmdUniq(tokens)
                Case "cut"
                    CmdCut(tokens)
                Case "tr"
                    CmdTr(tokens)
                Case "tee"
                    CmdTee(tokens)
                Case "sed"
                    CmdSed(tokens)
                Case "awk"
                    CmdAwk(tokens)

                '  ---- System-info commands ----
                Case "date"
                    CmdDate(tokens)
                Case "uname"
                    CmdUname(tokens)
                Case "hostname"
                    CmdHostname(tokens)
                Case "env"
                    CmdEnv(tokens)
                Case "export"
                    CmdExport(tokens)
                Case "id"
                    CmdId(tokens)
                Case "uptime"
                    CmdUptime(tokens)
                Case "ps"
                    CmdPs(tokens)
                Case "free"
                    CmdFree(tokens)
                Case "df"
                    CmdDf(tokens)
                Case "top"
                    CmdTop(tokens)
                Case "who"
                    CmdWho(tokens)

                '  ---- Shell builtins ----
                Case "alias"
                    CmdAlias(tokens)
                Case "unalias"
                    CmdUnalias(tokens)
                Case "history"
                    CmdHistory(tokens)
                Case "which"
                    CmdWhich(tokens)
                Case "type"
                    CmdType(tokens)
                Case "set"
                    CmdSet(tokens)
                Case "unset"
                    CmdUnset(tokens)
                Case "source", "."
                    CmdSource(tokens)

                '  ---- Utility commands ----
                Case "man"
                    CmdMan(tokens)
                Case "tree"
                    CmdTree(tokens)
                Case "less", "more"
                    CmdLess(tokens)
                Case "sleep"
                    CmdSleep(tokens)
                Case "cal"
                    CmdCal(tokens)

                Case Else
                    RaiseErrorEvent($"bash: {cmd}: command not found{Environment.NewLine}")
                    ShowPrompt()
            End Select
        Catch ex As Exception
            '  Catch-all to keep the session alive on unexpected errors.
            RaiseErrorEvent($"error: {ex.Message}{Environment.NewLine}")
            ShowPrompt()
        End Try
    End Sub

    ''' <summary>
    ''' Expands the leading command token if it matches a known alias. Only a
    ''' single (non-recursive) pass is performed so an alias can never trigger
    ''' infinite expansion.
    ''' </summary>
    Private Function ExpandAlias(line As String) As String
        If String.IsNullOrWhiteSpace(line) Then Return line

        Dim firstSpace As Integer = line.IndexOfAny(New Char() {" "c, ControlChars.Tab})
        Dim name As String
        Dim rest As String
        Dim value As String = Nothing

        If firstSpace < 0 Then
            name = line
            rest = String.Empty
        Else
            name = line.Substring(0, firstSpace)
            rest = line.Substring(firstSpace)
        End If

        If _aliases.TryGetValue(name, value) Then
            Return value & rest
        End If

        Return line
    End Function

    ''' <summary>
    ''' Handles the control keys the renderer cannot deal with on its own, because
    ''' they need shell state: completion needs the command table and the working
    ''' directory, Ctrl+C needs to abandon the pending line and reprint the prompt.
    ''' Anything else is ignored - there is no process to forward it to.
    ''' </summary>
    Public Overrides Sub WriteRaw(input As String)
        If Not _running OrElse String.IsNullOrEmpty(input) Then Return

        Select Case input
            Case vbTab
                CompleteCurrentLine()

            Case ChrW(3)    '  Ctrl+C - abandon the line.
                RaiseOutputEvent("^C" & Environment.NewLine)
                ClearEditorLine()
                ShowPrompt()

            Case ChrW(12)   '  Ctrl+L - clear the screen, keep what was typed.
                Dim pending As String = _editorLine

                RaiseOutputEvent(ChrW(&H1B) & "[2J" & ChrW(&H1B) & "[H")
                ShowPrompt()

                If pending.Length > 0 Then
                    '  ShowPrompt does not know about the pending line, so echo it
                    '  back and let the renderer re-adopt it.
                    RaiseOutputEvent(pending)
                    RaiseSetInputLineEvent(pending)
                End If

            Case Else
                '  Unrecognised sequence: silently ignored.
        End Select
    End Sub

#End Region

#Region "Line editing support"

    '''  The renderer's uncommitted input line and caret, pushed in just before
    '''  every raw keystroke so completion can work against what is on screen.
    Private _editorLine As String = String.Empty
    Private _editorCursor As Integer

    ''' <summary>
    ''' A local shell has no PTY, so the renderer should echo and edit lines
    ''' itself and only submit them once complete.
    ''' </summary>
    Public Overrides ReadOnly Property PreferredInputMode As ConsoleInputMode
        Get
            Return ConsoleInputMode.LineEdit
        End Get
    End Property

    Public Sub SetEditorState(line As String, cursorPosition As Integer) Implements IEditableInputLine.SetEditorState
        _editorLine = If(line, String.Empty)
        _editorCursor = System.Math.Max(0, System.Math.Min(_editorLine.Length, cursorPosition))
    End Sub

    ''' <summary>
    ''' Drops the pending line on both sides of the channel.
    ''' </summary>
    Private Sub ClearEditorLine()
        _editorLine = String.Empty
        _editorCursor = 0
        RaiseSetInputLineEvent(String.Empty)
    End Sub

    ''' <summary>
    ''' Tab completion. The first token completes against the built-in command
    ''' names, later tokens against the file system. A single candidate is applied
    ''' straight away; several are listed and the line is reprinted underneath, the
    ''' way bash does it.
    ''' </summary>
    Private Sub CompleteCurrentLine()
        '  Only completing at the end of the line keeps the rewrite unambiguous.
        Dim line As String = _editorLine

        If _editorCursor < line.Length Then Return

        Dim prefixStart As Integer = line.LastIndexOfAny(New Char() {" "c, ControlChars.Tab}) + 1
        Dim stem As String = line.Substring(prefixStart)
        Dim isCommandSlot As Boolean = prefixStart = 0

        Dim candidates As List(Of String) = If(isCommandSlot,
            CompleteCommand(stem),
            CompletePath(stem))

        If candidates.Count = 0 Then Return

        If candidates.Count = 1 Then
            ApplyCompletion(line, prefixStart, candidates(0))
            Return
        End If

        '  Extend as far as the candidates agree before showing the list; that is
        '  what makes repeated Tab presses feel responsive.
        Dim common As String = LongestCommonPrefix(candidates)

        If common.Length > stem.Length Then
            ApplyCompletion(line, prefixStart, common, appendSpace:=False)
            Return
        End If

        RaiseOutputEvent(Environment.NewLine)
        RaiseOutputEvent(FormatCandidateColumns(candidates))
        ShowPrompt()
        RaiseOutputEvent(line)
        RaiseSetInputLineEvent(line)
    End Sub

    ''' <summary>
    ''' Rewrites the tail of the line with <paramref name="completion"/> and tells
    ''' the renderer, so the screen and its line buffer cannot drift apart.
    ''' </summary>
    Private Sub ApplyCompletion(line As String,
                                prefixStart As Integer,
                                completion As String,
                                Optional appendSpace As Boolean = True)

        Dim completed As String = line.Substring(0, prefixStart) & completion

        '  A trailing separator already implies "keep going", so no space there.
        If appendSpace AndAlso
           Not completion.EndsWith(Path.DirectorySeparatorChar) AndAlso
           Not completion.EndsWith("/"c) Then
            completed &= " "
        End If

        If completed = line Then Return

        _editorLine = completed
        _editorCursor = completed.Length

        RaiseSetInputLineEvent(completed)
    End Sub

    Private Shared ReadOnly BuiltinCommands As String() = {
        "cat", "cd", "clear", "cp", "df", "echo", "env", "exit", "export",
        "file", "find", "free", "grep", "head", "help", "history", "hostname",
        "id", "la", "less", "ll", "ln", "ls", "l", "man", "mkdir", "more",
        "mv", "ps", "pwd", "rm", "sed", "set", "sleep", "sort", "source",
        "ssh", "tail", "tee", "top", "touch", "tr", "tree", "type", "uname",
        "unalias", "uniq", "uptime", "wc", "who", "whoami", "which"
    }

    Private Shared Function CompleteCommand(stem As String) As List(Of String)
        Return BuiltinCommands _
            .Where(Function(name) name.StartsWith(stem, StringComparison.OrdinalIgnoreCase)) _
            .ToList()
    End Function

    ''' <summary>
    ''' Completes a file-system stem against a single directory level. Deliberately
    ''' not recursive, and capped, so completing in a large tree cannot stall the
    ''' UI thread.
    ''' </summary>
    Private Function CompletePath(stem As String) As List(Of String)
        Const maxCandidates As Integer = 200

        Dim results As New List(Of String)()

        Try
            '  Split the stem into "directory part we search" and "name part we
            '  match", keeping the directory part verbatim so the completion can be
            '  spliced back onto the line unchanged.
            Dim separator As Integer = stem.LastIndexOfAny(New Char() {"/"c, "\"c})
            Dim typedDirectory As String = If(separator >= 0, stem.Substring(0, separator + 1), String.Empty)
            Dim namePrefix As String = If(separator >= 0, stem.Substring(separator + 1), stem)

            Dim searchRoot As String = If(typedDirectory.Length = 0, _cwd, ResolvePath(typedDirectory))

            If Not Directory.Exists(searchRoot) Then Return results

            For Each entry As String In Directory.EnumerateFileSystemEntries(searchRoot)
                Dim name As String = Path.GetFileName(entry)

                If Not name.StartsWith(namePrefix, StringComparison.OrdinalIgnoreCase) Then Continue For

                '  Directories get a trailing separator so the next Tab descends.
                If Directory.Exists(entry) Then
                    name &= Path.DirectorySeparatorChar
                End If

                results.Add(typedDirectory & name)

                If results.Count >= maxCandidates Then Exit For
            Next
        Catch
            '  Unreadable or over-long paths simply yield no completion; surfacing
            '  the exception here would paint an error over the user's line.
            results.Clear()
        End Try

        results.Sort(StringComparer.OrdinalIgnoreCase)

        Return results
    End Function

    Private Shared Function LongestCommonPrefix(values As List(Of String)) As String
        If values.Count = 0 Then Return String.Empty

        Dim prefix As String = values(0)

        For i As Integer = 1 To values.Count - 1
            Dim candidate As String = values(i)
            Dim length As Integer = 0

            While length < prefix.Length AndAlso
                  length < candidate.Length AndAlso
                  Char.ToLowerInvariant(prefix(length)) = Char.ToLowerInvariant(candidate(length))
                length += 1
            End While

            prefix = prefix.Substring(0, length)

            If prefix.Length = 0 Then Exit For
        Next

        Return prefix
    End Function

    ''' <summary>
    ''' Lays the candidates out in even columns, as a shell would.
    ''' </summary>
    Private Shared Function FormatCandidateColumns(candidates As List(Of String)) As String
        Const lineWidth As Integer = 80

        Dim widest As Integer = candidates.Max(Function(c) c.Length) + 2
        Dim columns As Integer = System.Math.Max(1, lineWidth \ widest)

        Dim sb As New StringBuilder()

        For i As Integer = 0 To candidates.Count - 1
            sb.Append(candidates(i).PadRight(widest))

            If (i + 1) Mod columns = 0 Then
                sb.Append(Environment.NewLine)
            End If
        Next

        If candidates.Count Mod columns <> 0 Then
            sb.Append(Environment.NewLine)
        End If

        Return sb.ToString()
    End Function

#End Region

#Region "Command implementations"

    Private Sub CmdPwd()
        RaiseOutputEvent(_cwd & Environment.NewLine)
        ShowPrompt()
    End Sub

    Private Sub CmdLs(tokens As String())
        Dim showLong As Boolean = tokens.Length > 1 AndAlso tokens(1) = "-l"
        Dim target As String = _cwd

        '  Resolve optional path argument (after possible -l flag).
        If showLong AndAlso tokens.Length > 2 Then
            target = ResolvePath(tokens(2))
        ElseIf Not showLong AndAlso tokens.Length > 1 Then
            target = ResolvePath(tokens(1))
        End If

        If Not Directory.Exists(target) Then
            RaiseErrorEvent($"ls: cannot access '{target}': No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim sb As New StringBuilder()

        If showLong Then
            Try
                '  Directories first, then files (bash convention).
                For Each d In Directory.GetDirectories(target)
                    Dim di = New DirectoryInfo(d)
                    sb.AppendLine($"d {di.LastWriteTime:MMM dd HH:mm}  {di.Name}/")
                Next
                For Each f In Directory.GetFiles(target)
                    Dim fi = New FileInfo(f)
                    sb.AppendLine($"- {fi.LastWriteTime:MMM dd HH:mm}  {fi.Name}")
                Next
            Catch ex As Exception
                RaiseErrorEvent($"ls: {ex.Message}{Environment.NewLine}")
                ShowPrompt()
                Return
            End Try
        Else
            Dim names As New List(Of String)()
            Try
                For Each d In Directory.GetDirectories(target)
                    names.Add(New DirectoryInfo(d).Name & "/")
                Next
                For Each f In Directory.GetFiles(target)
                    names.Add(Path.GetFileName(f))
                Next
            Catch ex As Exception
                RaiseErrorEvent($"ls: {ex.Message}{Environment.NewLine}")
                ShowPrompt()
                Return
            End Try

            If names.Count = 0 Then
                ShowPrompt()
                Return
            End If

            '  Simple multi-column output — space-separated.
            sb.AppendLine(String.Join("  ", names))
        End If

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdCd(tokens As String())
        If tokens.Length < 2 Then
            '  cd with no argument → go to home.
            _cwd = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        Else
            Dim target As String = ResolvePath(tokens(1))
            If Directory.Exists(target) Then
                _cwd = Path.GetFullPath(target)
            Else
                RaiseErrorEvent($"cd: {tokens(1)}: No such file or directory{Environment.NewLine}")
            End If
        End If

        Try
            Directory.SetCurrentDirectory(_cwd)
        Catch
        End Try

        ShowPrompt()
    End Sub

    Private Sub CmdCat(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"cat: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim target As String = ResolvePath(tokens(1))
        If Not File.Exists(target) Then
            RaiseErrorEvent($"cat: {tokens(1)}: No such file{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Try
            RaiseOutputEvent(File.ReadAllText(target))
            '  Ensure a trailing newline.
            If Not File.ReadAllText(target).EndsWith(vbLf) Then
                RaiseOutputEvent(Environment.NewLine)
            End If
        Catch ex As Exception
            RaiseErrorEvent($"cat: {tokens(1)}: {ex.Message}{Environment.NewLine}")
        End Try

        ShowPrompt()
    End Sub

    Private Sub CmdEcho(tokens As String())
        '  Echo everything after "echo".
        Dim idx As Integer = tokens(0).Length
        Dim raw = ""
        If idx < inputLine.Length Then
            raw = inputLine.Substring(idx).TrimStart()
        End If

        '  Strip surrounding quotes if present.
        If raw.Length >= 2 Then
            If (raw.StartsWith(""""c) AndAlso raw.EndsWith(""""c)) OrElse
               (raw.StartsWith("'"c) AndAlso raw.EndsWith("'"c)) Then
                raw = raw.Substring(1, raw.Length - 2)
            End If
        End If

        RaiseOutputEvent(raw & Environment.NewLine)
        ShowPrompt()
    End Sub

    Private Sub CmdMkdir(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"mkdir: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim target As String = ResolvePath(tokens(1))

        Try
            Directory.CreateDirectory(target)
        Catch ex As Exception
            RaiseErrorEvent($"mkdir: cannot create directory '{tokens(1)}': {ex.Message}{Environment.NewLine}")
        End Try

        ShowPrompt()
    End Sub

    Private Sub CmdRm(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"rm: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim target As String = ResolvePath(tokens(1))

        Try
            If Directory.Exists(target) Then
                Directory.Delete(target, recursive:=True)
            ElseIf File.Exists(target) Then
                File.Delete(target)
            Else
                RaiseErrorEvent($"rm: cannot remove '{tokens(1)}': No such file or directory{Environment.NewLine}")
            End If
        Catch ex As Exception
            RaiseErrorEvent($"rm: cannot remove '{tokens(1)}': {ex.Message}{Environment.NewLine}")
        End Try

        ShowPrompt()
    End Sub

    Private Sub CmdClear()
        '  ANSI clear-screen: Erase In Display (2) + Cursor Position (1;1).
        RaiseOutputEvent(ChrW(&H1B) & "[2J" & ChrW(&H1B) & "[H")
        ShowPrompt()
    End Sub

    Private Sub CmdWhoami()
        RaiseOutputEvent(Environment.UserName & Environment.NewLine)
        ShowPrompt()
    End Sub

    Private Sub CmdHelp()
        Dim sb As New StringBuilder()
        sb.AppendLine("Available commands (local shell):")
        sb.AppendLine("")
        sb.AppendLine("  Files / text:")
        sb.AppendLine("    pwd                       Print working directory")
        sb.AppendLine("    ls [-l] [-a] [path]       List directory contents")
        sb.AppendLine("    cd [dir]                  Change directory (default: home)")
        sb.AppendLine("    cat <file>                Print file contents")
        sb.AppendLine("    cp [-r] SRC DEST          Copy files / directories")
        sb.AppendLine("    mv SRC DEST               Move (rename) files")
        sb.AppendLine("    mkdir <dir>               Create a directory")
        sb.AppendLine("    rm [-r] <path>            Remove file or directory")
        sb.AppendLine("    touch <file>...           Create empty file / update time")
        sb.AppendLine("    ln [-s] TARGET LINK       Create links")
        sb.AppendLine("    head [-n N] FILE          Show first N lines")
        sb.AppendLine("    tail [-n N] FILE          Show last N lines")
        sb.AppendLine("    grep [-inv] PAT FILE...   Search text by pattern")
        sb.AppendLine("    wc [-lwc] FILE            Count lines/words/bytes")
        sb.AppendLine("    find [path] [-name P]     Search files recursively")
        sb.AppendLine("    file <file>               Determine file type")
        sb.AppendLine("    sort [-nru] FILE          Sort lines")
        sb.AppendLine("    uniq FILE                 Drop duplicate adjacent lines")
        sb.AppendLine("    cut -d D -f F FILE        Cut selected fields")
        sb.AppendLine("    tr SET1 SET2 FILE         Translate characters")
        sb.AppendLine("    sed 's/A/B/[g]' FILE      Simple substitution")
        sb.AppendLine("    awk -F S '/p/{print $N}' F  Field extraction")
        sb.AppendLine("")
        sb.AppendLine("  System info:")
        sb.AppendLine("    date                      Show current date/time")
        sb.AppendLine("    uname [-a]                System information")
        sb.AppendLine("    hostname                  Print host name")
        sb.AppendLine("    whoami                    Print current user name")
        sb.AppendLine("    who                       Show who is logged in")
        sb.AppendLine("    id                        Print user/group IDs")
        sb.AppendLine("    uptime                    How long the system has run")
        sb.AppendLine("    env                       List environment variables")
        sb.AppendLine("    export [NAME=VAL]         Set environment variables")
        sb.AppendLine("    ps [-ef|aux]              Snapshot of processes (simulated)")
        sb.AppendLine("    free [-h]                 Memory usage (simulated)")
        sb.AppendLine("    df [-h]                   Disk space usage")
        sb.AppendLine("    top                       Process overview (snapshot)")
        sb.AppendLine("")
        sb.AppendLine("  Shell builtins:")
        sb.AppendLine("    alias [NAME[=VAL]]        Define / list aliases")
        sb.AppendLine("    unalias NAME              Remove an alias")
        sb.AppendLine("    history                   Show command history")
        sb.AppendLine("    which <cmd>               Show command location")
        sb.AppendLine("    type <cmd>                Describe a command")
        sb.AppendLine("    set [NAME=VAL]...         Set / list variables")
        sb.AppendLine("    unset NAME...             Remove variables")
        sb.AppendLine("    source|. <file>           Run commands from a file")
        sb.AppendLine("")
        sb.AppendLine("  Utilities:")
        sb.AppendLine("    man <cmd>                 Show a short manual page")
        sb.AppendLine("    tree [path]               Display directory tree")
        sb.AppendLine("    less|more <file>          Page through a file")
        sb.AppendLine("    sleep N                   Wait N seconds")
        sb.AppendLine("    cal [[mon] year]          Show a calendar")
        sb.AppendLine("    clear                     Clear the screen")
        sb.AppendLine("    help                      Show this help")
        sb.AppendLine("    exit                      End the local session")
        sb.AppendLine("")
        sb.AppendLine("  ssh [-p port] [-pw pwd] [-i key] user@host  Connect to remote SSH")
        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdExit()
        _running = False
        RaiseOutputEvent("bye" & Environment.NewLine)
        RaiseExitEvent()
    End Sub

#End Region

#Region "File / text command implementations"

    Private Sub CmdCp(tokens As String())
        Dim recursive As Boolean = False
        Dim rest As New List(Of String)()

        For i As Integer = 1 To tokens.Length - 1
            If tokens(i) = "-r" OrElse tokens(i) = "-R" OrElse tokens(i) = "-a" OrElse tokens(i) = "-rf" OrElse tokens(i) = "-fr" Then
                recursive = True
            Else
                rest.Add(tokens(i))
            End If
        Next

        If rest.Count < 2 Then
            RaiseErrorEvent($"cp: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim sources = rest.GetRange(0, rest.Count - 1)
        Dim dest = rest(rest.Count - 1)
        Dim destPath = ResolvePath(dest)
        Dim destIsDir = Directory.Exists(destPath)

        For Each src In sources
            Dim srcPath = ResolvePath(src)
            Try
                If Directory.Exists(srcPath) Then
                    If Not recursive Then
                        RaiseErrorEvent($"cp: -r not specified; omitting directory '{src}'{Environment.NewLine}")
                        Continue For
                    End If
                    Dim target As String = If(destIsDir, Path.Combine(destPath, New DirectoryInfo(srcPath).Name), destPath)
                    CopyDirectory(srcPath, target, recursive)
                ElseIf File.Exists(srcPath) Then
                    Dim target As String = If(destIsDir, Path.Combine(destPath, Path.GetFileName(srcPath)), destPath)
                    File.Copy(srcPath, target, overwrite:=True)
                Else
                    RaiseErrorEvent($"cp: cannot stat '{src}': No such file or directory{Environment.NewLine}")
                End If
            Catch ex As Exception
                RaiseErrorEvent($"cp: {ex.Message}{Environment.NewLine}")
            End Try
        Next

        ShowPrompt()
    End Sub

    Private Sub CopyDirectory(source As String, dest As String, recursive As Boolean)
        Directory.CreateDirectory(dest)
        For Each f In Directory.GetFiles(source)
            File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), overwrite:=True)
        Next
        If recursive Then
            For Each d In Directory.GetDirectories(source)
                CopyDirectory(d, Path.Combine(dest, New DirectoryInfo(d).Name), recursive)
            Next
        End If
    End Sub

    Private Sub CmdMv(tokens As String())
        If tokens.Length < 3 Then
            RaiseErrorEvent($"mv: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim sources = tokens.Skip(1).Take(tokens.Length - 2).ToList()
        Dim dest = tokens(tokens.Length - 1)
        Dim destPath = ResolvePath(dest)
        Dim destIsDir = Directory.Exists(destPath)

        For Each src In sources
            Dim srcPath = ResolvePath(src)
            Try
                If Not (File.Exists(srcPath) OrElse Directory.Exists(srcPath)) Then
                    RaiseErrorEvent($"mv: cannot stat '{src}': No such file or directory{Environment.NewLine}")
                    Continue For
                End If
                Dim target As String = If(destIsDir, Path.Combine(destPath, Path.GetFileName(srcPath)), destPath)
                If Directory.Exists(srcPath) Then
                    Directory.Move(srcPath, target)
                Else
                    File.Move(srcPath, target)
                End If
            Catch ex As Exception
                RaiseErrorEvent($"mv: {ex.Message}{Environment.NewLine}")
            End Try
        Next

        ShowPrompt()
    End Sub

    Private Sub CmdTouch(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"touch: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        For i As Integer = 1 To tokens.Length - 1
            Dim p = ResolvePath(tokens(i))
            Try
                If Not File.Exists(p) Then
                    File.Create(p).Dispose()
                Else
                    File.SetLastWriteTime(p, Date.Now)
                End If
            Catch ex As Exception
                RaiseErrorEvent($"touch: cannot touch '{tokens(i)}': {ex.Message}{Environment.NewLine}")
            End Try
        Next

        ShowPrompt()
    End Sub

    Private Sub CmdHead(tokens As String())
        Dim lines As Integer = 10
        Dim fileIdx As Integer = -1

        For i As Integer = 1 To tokens.Length - 1
            If tokens(i) = "-n" AndAlso i + 1 < tokens.Length AndAlso Integer.TryParse(tokens(i + 1), lines) Then
                i += 1
            ElseIf tokens(i).StartsWith("-n") AndAlso Integer.TryParse(tokens(i).Substring(2), lines) Then
                '  -nN form
            ElseIf fileIdx < 0 Then
                fileIdx = i
            End If
        Next

        If fileIdx < 0 Then
            RaiseErrorEvent($"head: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim p = ResolvePath(tokens(fileIdx))
        If Not File.Exists(p) Then
            RaiseErrorEvent($"head: {tokens(fileIdx)}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Try
            Dim all = File.ReadAllLines(p)
            Dim take = System.Math.Min(lines, all.Length)
            Dim sb As New StringBuilder()
            For i As Integer = 0 To take - 1
                sb.AppendLine(all(i))
            Next
            RaiseOutputEvent(sb.ToString())
        Catch ex As Exception
            RaiseErrorEvent($"head: {ex.Message}{Environment.NewLine}")
        End Try

        ShowPrompt()
    End Sub

    Private Sub CmdTail(tokens As String())
        Dim lines As Integer = 10
        Dim fileIdx As Integer = -1
        Dim follow As Boolean = False

        For i As Integer = 1 To tokens.Length - 1
            If tokens(i) = "-n" AndAlso i + 1 < tokens.Length AndAlso Integer.TryParse(tokens(i + 1), lines) Then
                i += 1
            ElseIf tokens(i).StartsWith("-n") AndAlso Integer.TryParse(tokens(i).Substring(2), lines) Then
            ElseIf tokens(i) = "-f" OrElse tokens(i) = "--follow" Then
                follow = True
            ElseIf fileIdx < 0 Then
                fileIdx = i
            End If
        Next

        If fileIdx < 0 Then
            RaiseErrorEvent($"tail: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim p = ResolvePath(tokens(fileIdx))
        If Not File.Exists(p) Then
            RaiseErrorEvent($"tail: {tokens(fileIdx)}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Try
            Dim all = File.ReadAllLines(p)
            Dim startIdx = System.Math.Max(0, all.Length - lines)
            Dim sb As New StringBuilder()
            For i As Integer = startIdx To all.Length - 1
                sb.AppendLine(all(i))
            Next
            RaiseOutputEvent(sb.ToString())
            If follow Then
                RaiseOutputEvent("(follow mode not supported in local shell)" & Environment.NewLine)
            End If
        Catch ex As Exception
            RaiseErrorEvent($"tail: {ex.Message}{Environment.NewLine}")
        End Try

        ShowPrompt()
    End Sub

    Private Sub CmdGrep(tokens As String())
        '  grep [options] PATTERN [FILE...]
        Dim pattern As String = Nothing
        Dim files As New List(Of String)()
        Dim ignoreCase As Boolean = False
        Dim showLineNum As Boolean = False
        Dim invert As Boolean = False

        Dim i As Integer = 1
        While i < tokens.Length
            Dim t = tokens(i)
            If t.StartsWith("-") AndAlso t <> "-" Then
                For Each c In t.Substring(1)
                    Select Case c
                        Case "i"c : ignoreCase = True
                        Case "n"c : showLineNum = True
                        Case "v"c : invert = True
                        Case "r"c, "R"c  '  recursive (applied per-dir)
                        Case Else
                    End Select
                Next
                i += 1
            ElseIf pattern Is Nothing Then
                pattern = t
                i += 1
            Else
                files.Add(t)
                i += 1
            End If
        End While

        If pattern Is Nothing Then
            RaiseErrorEvent($"grep: missing pattern{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        If files.Count = 0 Then
            RaiseErrorEvent($"grep: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim opts As RegexOptions = If(ignoreCase, RegexOptions.IgnoreCase, RegexOptions.None)
        Dim rx As Regex
        Try
            rx = New Regex(pattern, opts)
        Catch ex As Exception
            RaiseErrorEvent($"grep: invalid pattern: {ex.Message}{Environment.NewLine}")
            ShowPrompt()
            Return
        End Try

        Dim sb As New StringBuilder()
        For Each f In files
            Dim fp = ResolvePath(f)
            If Directory.Exists(fp) Then
                For Each file In Directory.EnumerateFiles(fp, "*", SearchOption.AllDirectories)
                    GrepFile(rx, file, showLineNum, invert, files.Count > 1, sb)
                Next
            ElseIf File.Exists(fp) Then
                GrepFile(rx, fp, showLineNum, invert, files.Count > 1, sb)
            Else
                RaiseErrorEvent($"grep: {f}: No such file or directory{Environment.NewLine}")
            End If
        Next

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub GrepFile(rx As Regex, path As String, showLineNum As Boolean, invert As Boolean, showName As Boolean, sb As StringBuilder)
        Try
            Dim lines = File.ReadAllLines(path)
            For n As Integer = 0 To lines.Length - 1
                Dim match As Boolean = rx.IsMatch(lines(n))
                If match <> invert Then
                    If showName Then sb.Append(System.IO.Path.GetFileName(path) & ":")
                    If showLineNum Then sb.Append((n + 1) & ":")
                    sb.AppendLine(lines(n))
                End If
            Next
        Catch ex As Exception
            sb.AppendLine($"grep: {System.IO.Path.GetFileName(path)}: {ex.Message}")
        End Try
    End Sub

    Private Sub CmdWc(tokens As String())
        Dim countLines As Boolean = True
        Dim countWords As Boolean = True
        Dim countChars As Boolean = True
        Dim files As New List(Of String)()

        For i As Integer = 1 To tokens.Length - 1
            Select Case tokens(i)
                Case "-l" : countLines = True : countWords = False : countChars = False
                Case "-w" : countWords = True : countLines = False : countChars = False
                Case "-c", "-m" : countChars = True : countLines = False : countWords = False
                Case Else : files.Add(tokens(i))
            End Select
        Next

        If files.Count = 0 Then
            RaiseErrorEvent($"wc: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim totalL As Long = 0, totalW As Long = 0, totalC As Long = 0
        Dim sb As New StringBuilder()

        For Each f In files
            Dim fp = ResolvePath(f)
            If Not File.Exists(fp) Then
                RaiseErrorEvent($"wc: {f}: No such file or directory{Environment.NewLine}")
                Continue For
            End If
            Dim allLines = File.ReadAllLines(fp)
            Dim content = File.ReadAllText(fp)
            Dim l As Long = allLines.Length
            If content.Length > 0 AndAlso Not content.EndsWith(vbLf) Then l += 1
            Dim w As Long = content.Split(New Char() {" "c, vbTab(0), vbLf(0), vbCr(0)}, StringSplitOptions.RemoveEmptyEntries).Length
            Dim c As Long = content.Length
            totalL += l : totalW += w : totalC += c
            Dim parts As New List(Of String)()
            If countLines Then parts.Add(l.ToString().PadLeft(7))
            If countWords Then parts.Add(w.ToString().PadLeft(7))
            If countChars Then parts.Add(c.ToString().PadLeft(7))
            sb.AppendLine(String.Join(" ", parts) & " " & f)
        Next

        If files.Count > 1 Then
            Dim parts As New List(Of String)()
            If countLines Then parts.Add(totalL.ToString().PadLeft(7))
            If countWords Then parts.Add(totalW.ToString().PadLeft(7))
            If countChars Then parts.Add(totalC.ToString().PadLeft(7))
            sb.AppendLine(String.Join(" ", parts) & " total")
        End If

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdFind(tokens As String())
        '  find [path] [-name PATTERN] [-type f|d]
        If tokens.Length < 2 Then
            RaiseErrorEvent($"find: missing path operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim startPath As String = ResolvePath(tokens(1))
        Dim namePattern As String = Nothing
        Dim typeFilter As Char = Nothing

        Dim i As Integer = 2
        While i < tokens.Length
            Select Case tokens(i)
                Case "-name"
                    If i + 1 < tokens.Length Then namePattern = tokens(i + 1) : i += 2
                Case "-type"
                    If i + 1 < tokens.Length Then typeFilter = tokens(i + 1)(0) : i += 2
                Case Else
                    i += 1
            End Select
        End While

        If Not Directory.Exists(startPath) Then
            RaiseErrorEvent($"find: '{tokens(1)}': No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim nameRx As Regex = Nothing
        If namePattern IsNot Nothing Then
            Try
                nameRx = New Regex("^" & Regex.Escape(namePattern).Replace("\*", ".*").Replace("\?", ".") & "$", RegexOptions.IgnoreCase)
            Catch
                nameRx = Nothing
            End Try
        End If

        Dim sb As New StringBuilder()
        Const maxDepth As Integer = 200

        Dim visit As Action(Of String, Integer) = Nothing
        visit = Sub(dir, depth)
                    If depth > maxDepth Then Return
                    Try
                        For Each entry In Directory.EnumerateFileSystemEntries(dir)
                            Dim isDir = Directory.Exists(entry)
                            If typeFilter = "f"c AndAlso isDir Then Continue For
                            If typeFilter = "d"c AndAlso Not isDir Then Continue For
                            If nameRx Is Nothing OrElse nameRx.IsMatch(Path.GetFileName(entry)) Then
                                sb.AppendLine(entry)
                            End If
                            If isDir Then visit(entry, depth + 1)
                        Next
                    Catch
                    End Try
                End Sub
        visit(startPath, 0)

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdFile(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"file: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim sb As New StringBuilder()
        For i As Integer = 1 To tokens.Length - 1
            Dim p = ResolvePath(tokens(i))
            If Not File.Exists(p) Then
                sb.AppendLine($"{tokens(i)}: cannot open (`No such file or directory')")
                Continue For
            End If
            Dim desc As String = "data"
            Try
                Using fs As New FileStream(p, FileMode.Open, FileAccess.Read)
                    Dim buf(3) As Byte
                    Dim n = fs.Read(buf, 0, buf.Length)
                    If n >= 2 AndAlso buf(0) = &H4D AndAlso buf(1) = &H5A Then
                        desc = "PE32 executable (GUI) Intel 80386, for MS Windows"
                    ElseIf n >= 4 AndAlso buf(0) = &H7F AndAlso buf(1) = &H45 AndAlso buf(2) = &H4C AndAlso buf(3) = &H46 Then
                        desc = "ELF executable"
                    ElseIf n >= 3 AndAlso buf(0) = &HEF AndAlso buf(1) = &HBB AndAlso buf(2) = &HBF Then
                        desc = "Unicode text, UTF-8"
                    ElseIf n >= 2 AndAlso buf(0) = &HFF AndAlso buf(1) = &HFE Then
                        desc = "Unicode text, UTF-16, little-endian"
                    ElseIf n >= 2 AndAlso buf(0) = &HFE AndAlso buf(1) = &HFF Then
                        desc = "Unicode text, UTF-16, big-endian"
                    Else
                        '  Heuristic: if all bytes are printable/space/line, call it text.
                        Dim allText As Boolean = True
                        For Each b In buf
                            If b < 9 OrElse (b > 13 AndAlso b < 32) Then allText = False : Exit For
                        Next
                        If allText Then desc = "ASCII text"
                    End If
                End Using
            Catch ex As Exception
                desc = "data"
            End Try
            sb.AppendLine($"{tokens(i)}: {desc}")
        Next

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdLn(tokens As String())
        '  ln [-s] TARGET LINK
        Dim symbolic As Boolean = False
        Dim rest As New List(Of String)()
        For i As Integer = 1 To tokens.Length - 1
            If tokens(i) = "-s" OrElse tokens(i) = "-sf" OrElse tokens(i) = "-fs" Then
                symbolic = True
            Else
                rest.Add(tokens(i))
            End If
        Next

        If rest.Count < 2 Then
            RaiseErrorEvent($"ln: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim target = ResolvePath(rest(0))
        Dim link = ResolvePath(rest(1))

        Try
            If symbolic Then
                If File.Exists(target) Then
                    File.CreateSymbolicLink(link, target)
                ElseIf Directory.Exists(target) Then
                    Directory.CreateSymbolicLink(link, target)
                Else
                    RaiseErrorEvent($"ln: failed to create symbolic link '{rest(1)}': No such file or directory{Environment.NewLine}")
                    ShowPrompt()
                    Return
                End If
            Else
                '  Hard link (files only).
                If File.Exists(target) Then
                    ' File.CreateHardLink(link, target)
                Else
                    RaiseErrorEvent($"ln: failed to create hard link '{rest(1)}': No such file or directory{Environment.NewLine}")
                    ShowPrompt()
                    Return
                End If
            End If
        Catch ex As Exception
            RaiseErrorEvent($"ln: {ex.Message}{Environment.NewLine}")
        End Try

        ShowPrompt()
    End Sub

    Private Sub CmdSort(tokens As String())
        Dim unique As Boolean = False
        Dim numeric As Boolean = False
        Dim reverse As Boolean = False
        Dim files As New List(Of String)()

        For i As Integer = 1 To tokens.Length - 1
            Select Case tokens(i)
                Case "-u" : unique = True
                Case "-n" : numeric = True
                Case "-r" : reverse = True
                Case Else : files.Add(tokens(i))
            End Select
        Next

        If files.Count = 0 Then
            RaiseErrorEvent($"sort: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim lines As New List(Of String)()
        For Each f In files
            Dim fp = ResolvePath(f)
            If Not File.Exists(fp) Then
                RaiseErrorEvent($"sort: cannot read '{f}': No such file or directory{Environment.NewLine}")
                Continue For
            End If
            lines.AddRange(File.ReadAllLines(fp))
        Next

        Dim comparer As IComparer(Of String)
        If numeric Then
            comparer = System.Collections.Generic.Comparer(Of String).Create(Function(a, b)
                                                                                 Dim da, db As Double
                                                                                 Dim pa = Double.TryParse(a, da)
                                                                                 Dim pb = Double.TryParse(b, db)
                                                                                 If pa AndAlso pb Then Return da.CompareTo(db)
                                                                                 Return String.Compare(a, b)
                                                                             End Function)
        Else
            comparer = StringComparer.Ordinal
        End If

        lines.Sort(comparer)
        If reverse Then lines.Reverse()

        Dim result = If(unique, lines.Distinct(StringComparer.Ordinal).ToList(), lines)
        RaiseOutputEvent(String.Join(Environment.NewLine, result) & If(result.Count > 0, Environment.NewLine, ""))
        ShowPrompt()
    End Sub

    Private Sub CmdUniq(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"uniq: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim fp = ResolvePath(tokens(1))
        If Not File.Exists(fp) Then
            RaiseErrorEvent($"uniq: {tokens(1)}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim lines = File.ReadAllLines(fp).ToList()
        Dim out As New List(Of String)()
        For i As Integer = 0 To lines.Count - 1
            If i = 0 OrElse lines(i) <> lines(i - 1) Then out.Add(lines(i))
        Next

        RaiseOutputEvent(String.Join(Environment.NewLine, out) & If(out.Count > 0, Environment.NewLine, ""))
        ShowPrompt()
    End Sub

    Private Sub CmdCut(tokens As String())
        '  cut -d DELIM -f FIELDS [FILE]
        Dim delim As Char = vbTab(0)
        Dim fields As String = Nothing
        Dim file As String = Nothing

        Dim i As Integer = 1
        While i < tokens.Length
            If tokens(i) = "-d" AndAlso i + 1 < tokens.Length Then
                delim = tokens(i + 1)(0) : i += 2
            ElseIf tokens(i).StartsWith("-d") Then
                delim = tokens(i)(2) : i += 1
            ElseIf tokens(i) = "-f" AndAlso i + 1 < tokens.Length Then
                fields = tokens(i + 1) : i += 2
            ElseIf tokens(i).StartsWith("-f") Then
                fields = tokens(i).Substring(2) : i += 1
            Else
                file = tokens(i) : i += 1
            End If
        End While

        If fields Is Nothing Then
            RaiseErrorEvent($"cut: missing -f option{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim fieldSet As New HashSet(Of Integer)()
        For Each part In fields.Split(","c)
            If part.Contains("-"c) Then
                Dim rng = part.Split("-"c)
                Dim lo As Integer = 0, hi As Integer = Integer.MaxValue
                If rng(0).Length > 0 Then Integer.TryParse(rng(0), lo)
                If rng.Length > 1 AndAlso rng(1).Length > 0 Then Integer.TryParse(rng(1), hi)
                For f As Integer = lo To hi : fieldSet.Add(f) : Next
            Else
                Dim f As Integer
                If Integer.TryParse(part, f) Then fieldSet.Add(f)
            End If
        Next

        If file Is Nothing Then
            RaiseErrorEvent($"cut: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim fp = ResolvePath(file)
        If Not System.IO.File.Exists(fp) Then
            RaiseErrorEvent($"cut: {file}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim sb As New StringBuilder()
        For Each line In System.IO.File.ReadAllLines(fp)
            Dim cols = line.Split(delim)
            Dim picked As New List(Of String)()
            For Each f In fieldSet
                If f >= 1 AndAlso f <= cols.Length Then picked.Add(cols(f - 1))
            Next
            sb.AppendLine(String.Join(delim, picked))
        Next

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdTr(del As String, file As String)
        Dim fp As String = ResolvePath(file)
        If Not System.IO.File.Exists(fp) Then
            RaiseErrorEvent($"tr: {file}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If
        Dim text = System.IO.File.ReadAllText(fp)
        For Each c In del
            text = text.Replace(c, "")
        Next
        RaiseOutputEvent(text)
        ShowPrompt()
        Return
    End Sub

    Private Sub CmdTr(tokens As String())
        '  tr SET1 SET2   (simplified: char-by-char translation / delete via -d)
        If tokens.Length < 2 Then
            RaiseErrorEvent($"tr: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        If tokens(1) = "-d" AndAlso tokens.Length >= 3 AndAlso tokens.Length >= 4 Then
            Call CmdTr(del:=tokens(2), file:=tokens(3))
        End If

        If tokens.Length < 3 Then
            RaiseErrorEvent($"tr: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim set1 = ExpandTrSet(tokens(1))
        Dim set2 = ExpandTrSet(tokens(2))
        Dim file = tokens(3)
        Dim fp = ResolvePath(file)
        If Not System.IO.File.Exists(fp) Then
            RaiseErrorEvent($"tr: {file}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim text = System.IO.File.ReadAllText(fp)
        Dim map As New Dictionary(Of Char, Char)()
        Dim minLen = System.Math.Min(set1.Length, set2.Length)
        For i As Integer = 0 To minLen - 1
            map(set1(i)) = set2(i)
        Next
        Dim sb As New StringBuilder()
        For Each c In text
            If map.ContainsKey(c) Then sb.Append(map(c)) Else sb.Append(c)
        Next

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Function ExpandTrSet(s As String) As String
        '  Supports simple ranges like a-z, A-Z, 0-9.
        Dim sb As New StringBuilder()
        Dim i As Integer = 0
        While i < s.Length
            If i + 2 < s.Length AndAlso s(i + 1) = "-"c Then
                Dim a = AscW(s(i)), b = AscW(s(i + 2))
                For c As Integer = a To b : sb.Append(ChrW(c)) : Next
                i += 3
            Else
                sb.Append(s(i))
                i += 1
            End If
        End While
        Return sb.ToString()
    End Function

    Private Sub CmdTee(tokens As String())
        '  tee [-a] FILE
        Dim append As Boolean = False
        Dim files As New List(Of String)()
        For i As Integer = 1 To tokens.Length - 1
            If tokens(i) = "-a" Then append = True Else files.Add(tokens(i))
        Next

        If files.Count = 0 Then
            RaiseErrorEvent($"tee: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        '  tee normally reads stdin; here we require the input to be piped, so
        '  we just echo a notice and refuse without a stream.
        RaiseErrorEvent($"tee: standard input not supported in local shell{Environment.NewLine}")
        ShowPrompt()
    End Sub

    Private Sub CmdSed(tokens As String())
        '  sed 's/FIND/REPLACE/[g]' FILE
        If tokens.Length < 3 Then
            RaiseErrorEvent($"sed: missing operand (usage: sed 's/old/new/[g]' file){Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim script = tokens(1)
        Dim file = tokens(2)
        Dim fp = ResolvePath(file)
        If Not System.IO.File.Exists(fp) Then
            RaiseErrorEvent($"sed: can't read {file}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        '  Parse s/.../.../g
        Dim m = Regex.Match(script, "^s/(.*)/(.*)/([g]*)$")
        If Not m.Success Then
            RaiseErrorEvent($"sed: unsupported script '{script}' (only s/F/R/[g] is supported){Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim find = m.Groups(1).Value
        Dim repl = m.Groups(2).Value.Replace("\/", "/")
        Dim globalReplace = m.Groups(3).Value.Contains("g")

        Dim rx As Regex
        Try
            rx = New Regex(find, RegexOptions.None)
        Catch ex As Exception
            RaiseErrorEvent($"sed: bad regex: {ex.Message}{Environment.NewLine}")
            ShowPrompt()
            Return
        End Try

        Dim sb As New StringBuilder()
        For Each line In System.IO.File.ReadAllLines(fp)
            If globalReplace Then
                sb.AppendLine(rx.Replace(line, repl))
            Else
                sb.AppendLine(rx.Replace(line, repl, 1))
            End If
        Next

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdAwk(tokens As String())
        '  awk -F SEP '/PATTERN/ {print $N}' FILE   (simplified subset)
        If tokens.Length < 3 Then
            RaiseErrorEvent($"awk: missing operand (usage: awk -F, '/pat/ {{print $1}}' file){Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim sep As Char = " "c
        Dim i As Integer = 1
        If tokens(i) = "-F" AndAlso i + 1 < tokens.Length Then
            sep = tokens(i + 1)(0) : i += 2
        End If

        Dim program = tokens(i) : i += 1
        Dim file = tokens(i)
        Dim fp = ResolvePath(file)
        If Not System.IO.File.Exists(fp) Then
            RaiseErrorEvent($"awk: can't read {file}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        '  Extract pattern and print fields.
        Dim patMatch = Regex.Match(program, "^/(.*)/\s*\{print\s*(.*)\}$")
        If Not patMatch.Success Then
            RaiseErrorEvent($"awk: unsupported program (only /pat/{{print $N}} is supported){Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim pattern As String = patMatch.Groups(1).Value
        Dim printSpec = patMatch.Groups(2).Value
        Dim rx As Regex = Nothing
        If pattern.Length > 0 Then
            Try
                rx = New Regex(pattern)
            Catch ex As Exception
                RaiseErrorEvent($"awk: bad regex: {ex.Message}{Environment.NewLine}")
                ShowPrompt()
                Return
            End Try
        End If

        Dim sb As New StringBuilder()
        For Each line In System.IO.File.ReadAllLines(fp)
            If rx IsNot Nothing AndAlso Not rx.IsMatch(line) Then Continue For
            Dim cols = line.Split(sep)
            Dim expanded = Regex.Replace(printSpec, "\$(\d+)", Function(mt)
                                                                   Dim idx As Integer
                                                                   If Integer.TryParse(mt.Groups(1).Value, idx) AndAlso idx >= 1 AndAlso idx <= cols.Length Then
                                                                       Return cols(idx - 1)
                                                                   End If
                                                                   Return mt.Value
                                                               End Function)
            sb.AppendLine(expanded)
        Next

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

#End Region

#Region "System-info command implementations"

    Private Sub CmdDate(tokens As String())
        Dim fmt As String = "ddd MMM dd HH:mm:ss UTCzzz yyyy"
        For i As Integer = 1 To tokens.Length - 1
            If tokens(i) = "-u" OrElse tokens(i) = "--utc" Then
                RaiseOutputEvent(DateTime.UtcNow.ToString("ddd MMM dd HH:mm:ss UTC yyyy") & Environment.NewLine)
                ShowPrompt()
                Return
            ElseIf tokens(i) = "+*" Then
                fmt = tokens(i).Substring(1)
            End If
        Next
        RaiseOutputEvent(DateTime.Now.ToString(fmt) & Environment.NewLine)
        ShowPrompt()
    End Sub

    Private Sub CmdUname(tokens As String())
        Dim all As Boolean = False
        Dim kernel As Boolean = False
        Dim nodename As Boolean = False
        Dim machine As Boolean = False
        Dim os As Boolean = False

        For i As Integer = 1 To tokens.Length - 1
            Select Case tokens(i)
                Case "-a" : all = True
                Case "-s" : kernel = True
                Case "-n" : nodename = True
                Case "-m" : machine = True
                Case "-o" : os = True
            End Select
        Next

        Dim parts As New List(Of String)()
        If all OrElse (Not kernel AndAlso Not nodename AndAlso Not machine AndAlso Not os) Then
            parts.Add("Windows")
            parts.Add(Environment.MachineName)
            parts.Add(Environment.OSVersion.VersionString)
            parts.Add(Environment.OSVersion.Platform.ToString())
            parts.Add("x86_64")
        Else
            If kernel Then parts.Add("Windows")
            If nodename Then parts.Add(Environment.MachineName)
            If os Then parts.Add(Environment.OSVersion.Platform.ToString())
            If machine Then parts.Add("x86_64")
        End If

        RaiseOutputEvent(String.Join(" ", parts) & Environment.NewLine)
        ShowPrompt()
    End Sub

    Private Sub CmdHostname(tokens As String())
        RaiseOutputEvent(Environment.MachineName & Environment.NewLine)
        ShowPrompt()
    End Sub

    Private Sub CmdEnv(tokens As String())
        Dim sb As New StringBuilder()
        For Each kv In _env.OrderBy(Function(k) k.Key, StringComparer.OrdinalIgnoreCase)
            sb.AppendLine($"{kv.Key}={kv.Value}")
        Next
        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdExport(tokens As String())
        '  export NAME=VALUE   OR   export NAME   OR   export (list all)
        If tokens.Length = 1 Then
            For Each kv In _env.OrderBy(Function(k) k.Key, StringComparer.OrdinalIgnoreCase)
                RaiseOutputEvent($"declare -x {kv.Key}=""{kv.Value}""" & Environment.NewLine)
            Next
            ShowPrompt()
            Return
        End If

        For i As Integer = 1 To tokens.Length - 1
            Dim eq = tokens(i).IndexOf("="c)
            If eq > 0 Then
                Dim name = tokens(i).Substring(0, eq)
                Dim value = tokens(i).Substring(eq + 1)
                _env(name) = value
            Else
                If Not _env.ContainsKey(tokens(i)) Then _env(tokens(i)) = ""
            End If
        Next

        ShowPrompt()
    End Sub

    Private Sub CmdId(tokens As String())
        Dim user = Environment.UserName
        Dim domain = Environment.UserDomainName
        RaiseOutputEvent($"uid=1000({user}) gid=1000({domain}) groups=1000({domain}),545(users)" & Environment.NewLine)
        ShowPrompt()
    End Sub

    Private Sub CmdUptime(tokens As String())
        Dim boot = DateTime.Now.AddMilliseconds(-Environment.TickCount)
        Dim up = DateTime.Now - boot
        Dim days = up.Days
        Dim hh = up.Hours
        Dim mm = up.Minutes
        Dim loadStr = $"{0.0:F2}, {0.0:F2}, {0.0:F2}"
        RaiseOutputEvent($" {DateTime.Now:HH:mm:ss}  up {days} days, {hh}:{mm:00},  1 user,  load average: {loadStr}" & Environment.NewLine)
        ShowPrompt()
    End Sub

    Private Sub CmdPs(tokens As String())
        Dim full As Boolean = tokens.Length > 1 AndAlso (tokens(1) = "aux" OrElse tokens(1) = "-ef" OrElse tokens(1) = "auxww")
        Dim procs = Process.GetProcesses()
        Dim sb As New StringBuilder()

        If full Then
            sb.AppendLine("USER       PID %CPU %MEM    VSZ   RSS TTY      STAT START   TIME COMMAND")
        Else
            sb.AppendLine("  PID TTY          TIME CMD")
        End If

        For Each p In procs
            Try
                Dim user = Environment.UserName
                If full Then
                    Dim ws As Long = 0
                    Try : ws = p.WorkingSet64 : Catch : End Try
                    sb.AppendLine($"{user,-10} {p.Id,5} {0,4:F0} {0,4:F0} {0,5} {ws,5} ?        Ss   {DateTime.Now:HH:mm}   0:00 {p.ProcessName}")
                Else
                    Dim t = TimeSpan.Zero
                    Try : t = p.TotalProcessorTime : Catch : End Try
                    sb.AppendLine($"{p.Id,5} ?        {t:hh\:mm\:ss} {p.ProcessName}")
                End If
            Catch
            End Try
        Next

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdFree(tokens As String())
        Dim human As Boolean = tokens.Length > 1 AndAlso (tokens(1) = "-h" OrElse tokens(1) = "-m" OrElse tokens(1) = "-g")
        Dim gc = System.GC.GetTotalMemory(False)
        Dim totalPhys As Long = 0, availPhys As Long = 0
        Try
            Dim ci = New Microsoft.VisualBasic.Devices.ComputerInfo()
            totalPhys = CLng(ci.TotalPhysicalMemory)
            availPhys = CLng(ci.AvailablePhysicalMemory)
        Catch
            totalPhys = 8 * 1024 * 1024 * 1024L
            availPhys = totalPhys \ 2
        End Try

        Dim usedPhys = totalPhys - availPhys

        Dim fmt As Func(Of Long, String)
        If tokens.Length > 1 AndAlso tokens(1) = "-g" Then
            fmt = Function(b) (b / 1073741824.0).ToString("F1") & "G"
        ElseIf tokens.Length > 1 AndAlso tokens(1) = "-m" Then
            fmt = Function(b) (b / 1048576.0).ToString("F0") & "M"
        Else
            fmt = Function(b) b.ToString()
        End If

        Dim sb As New StringBuilder()
        sb.AppendLine("              total        used        free      shared  buff/cache   available")
        sb.AppendLine($"Mem:        {fmt(totalPhys),11} {fmt(usedPhys),11} {fmt(availPhys),11} {fmt(0),11} {fmt(0),11} {fmt(availPhys),11}")
        sb.AppendLine($"Swap:       {fmt(0),11} {fmt(0),11} {fmt(0),11}")
        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdDf(tokens As String())
        Dim human As Boolean = tokens.Length > 1 AndAlso tokens(1) = "-h"
        Dim drives = DriveInfo.GetDrives().Where(Function(d) d.IsReady).ToList()
        Dim sb As New StringBuilder()
        sb.AppendLine("Filesystem      Size  Used Avail Use% Mounted on")

        For Each d In drives
            Try
                Dim total = d.TotalSize
                Dim free = d.TotalFreeSpace
                Dim used = total - free
                Dim pct = If(total > 0, CInt(used * 100 / total), 0)
                Dim sizeStr, usedStr, freeStr As String
                If human Then
                    sizeStr = HumanSize(total) : usedStr = HumanSize(used) : freeStr = HumanSize(free)
                Else
                    sizeStr = (total / 1024).ToString("F0") : usedStr = (used / 1024).ToString("F0") : freeStr = (free / 1024).ToString("F0")
                End If
                sb.AppendLine($"{d.Name,-15} {sizeStr,5} {usedStr,5} {freeStr,5} {pct,3}% {d.Name}")
            Catch
            End Try
        Next

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Shared Function HumanSize(bytes As Long) As String
        Dim units = {"B", "K", "M", "G", "T"}
        Dim v As Double = bytes
        Dim u As Integer = 0
        While v >= 1024 AndAlso u < units.Length - 1
            v /= 1024 : u += 1
        End While
        Return v.ToString("F1") & units(u)
    End Function

    Private Sub CmdTop(tokens As String())
        '  A single snapshot; interactive mode is not simulated.
        Dim sb As New StringBuilder()
        sb.AppendLine($"top - {DateTime.Now:HH:mm:ss} up ? min,  1 user,  load average: 0.00, 0.00, 0.00")
        sb.AppendLine($"Tasks: {Process.GetProcesses().Length} total,   1 running, {Process.GetProcesses().Length - 1} sleeping")
        sb.AppendLine("")
        sb.AppendLine("  PID USER      PR  NI    VIRT    RES  SHR %CPU %MEM     TIME+ COMMAND")

        Dim procs = Process.GetProcesses().OrderByDescending(Function(p)
                                                                 Try : Return p.WorkingSet64 : Catch : Return 0 : End Try
                                                             End Function).Take(15).ToList()

        For Each p In procs
            Dim ws As Long = 0
            Try : ws = p.WorkingSet64 : Catch : End Try
            sb.AppendLine($"{p.Id,5} {Environment.UserName,-9} {20,2} {0,3} {0,8} {ws,6} {0,4} {0.0,4:F1} {0.0,4:F1} {0:00:00.00} {p.ProcessName}")
        Next

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub CmdWho(tokens As String())
        RaiseOutputEvent($"{Environment.UserName}  console  {DateTime.Now:MMM dd HH:mm} (:0){Environment.NewLine}")
        ShowPrompt()
    End Sub

#End Region

#Region "Shell builtin command implementations"

    Private Sub CmdAlias(tokens As String())
        If tokens.Length <= 1 Then
            For Each kv In _aliases.OrderBy(Function(k) k.Key, StringComparer.OrdinalIgnoreCase)
                RaiseOutputEvent($"alias {kv.Key}='{kv.Value}'{Environment.NewLine}")
            Next
            ShowPrompt()
            Return
        End If

        For i As Integer = 1 To tokens.Length - 1
            Dim eq = tokens(i).IndexOf("="c)
            If eq > 0 Then
                Dim name = tokens(i).Substring(0, eq)
                Dim value = tokens(i).Substring(eq + 1).Trim("'"c, """"c)
                _aliases(name) = value
            Else
                If _aliases.ContainsKey(tokens(i)) Then
                    RaiseOutputEvent($"alias {tokens(i)}='{_aliases(tokens(i))}'{Environment.NewLine}")
                Else
                    RaiseErrorEvent($"alias: {tokens(i)}: not found{Environment.NewLine}")
                End If
            End If
        Next

        ShowPrompt()
    End Sub

    Private Sub CmdUnalias(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"unalias: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        For i As Integer = 1 To tokens.Length - 1
            If _aliases.ContainsKey(tokens(i)) Then
                _aliases.Remove(tokens(i))
            Else
                RaiseErrorEvent($"unalias: {tokens(i)}: not found{Environment.NewLine}")
            End If
        Next

        ShowPrompt()
    End Sub

    Private Sub CmdHistory(tokens As String())
        For i As Integer = 0 To _history.Count - 1
            RaiseOutputEvent($"  {i + 1}  {_history(i)}{Environment.NewLine}")
        Next
        ShowPrompt()
    End Sub

    Private Sub CmdWhich(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"which: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        For i As Integer = 1 To tokens.Length - 1
            If BuiltinCommands.Contains(tokens(i), StringComparer.OrdinalIgnoreCase) OrElse _aliases.ContainsKey(tokens(i)) Then
                RaiseOutputEvent($"/usr/bin/{tokens(i)}{Environment.NewLine}")
            Else
                RaiseErrorEvent($"which: no {tokens(i)} in (/usr/local/bin:/usr/bin:/bin){Environment.NewLine}")
            End If
        Next

        ShowPrompt()
    End Sub

    Private Sub CmdType(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"type: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        For i As Integer = 1 To tokens.Length - 1
            Dim name = tokens(i)
            If _aliases.ContainsKey(name) Then
                RaiseOutputEvent($"{name} is aliased to `{_aliases(name)}'{Environment.NewLine}")
            ElseIf BuiltinCommands.Contains(name, StringComparer.OrdinalIgnoreCase) Then
                RaiseOutputEvent($"{name} is a shell builtin{Environment.NewLine}")
            ElseIf _env.ContainsKey("PATH") Then
                RaiseOutputEvent($"{name} is /usr/bin/{name}{Environment.NewLine}")
            Else
                RaiseErrorEvent($"type: {name}: not found{Environment.NewLine}")
            End If
        Next

        ShowPrompt()
    End Sub

    Private Sub CmdSet(tokens As String())
        '  With no args, list all variables (env + a few shell vars).
        If tokens.Length = 1 Then
            Dim sb As New StringBuilder()
            For Each kv In _env.OrderBy(Function(k) k.Key, StringComparer.OrdinalIgnoreCase)
                sb.AppendLine($"{kv.Key}={kv.Value}")
            Next
            sb.AppendLine($"PWD={_cwd}")
            sb.AppendLine($"OLDPWD={_cwd}")
            RaiseOutputEvent(sb.ToString())
            ShowPrompt()
            Return
        End If

        '  set NAME=VALUE
        For i As Integer = 1 To tokens.Length - 1
            Dim eq = tokens(i).IndexOf("="c)
            If eq > 0 Then
                _env(tokens(i).Substring(0, eq)) = tokens(i).Substring(eq + 1)
            End If
        Next

        ShowPrompt()
    End Sub

    Private Sub CmdUnset(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"unset: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        For i As Integer = 1 To tokens.Length - 1
            If _env.ContainsKey(tokens(i)) Then _env.Remove(tokens(i))
        Next

        ShowPrompt()
    End Sub

    Private Sub CmdSource(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"source: missing filename{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim fp = ResolvePath(tokens(1))
        If Not File.Exists(fp) Then
            RaiseErrorEvent($"source: {tokens(1)}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Try
            For Each line In File.ReadAllLines(fp)
                Dim trimmed = line.Trim()
                If trimmed.Length = 0 OrElse trimmed.StartsWith("#"c) Then Continue For
                WriteInput(trimmed)
            Next
        Catch ex As Exception
            RaiseErrorEvent($"source: {ex.Message}{Environment.NewLine}")
            ShowPrompt()
        End Try
    End Sub

#End Region

#Region "Utility command implementations"

    Private Shared ReadOnly ManPages As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase) From {
        {"ls", "LS(1) — list directory contents. Usage: ls [-l] [-a] [path]"},
        {"cd", "CD(1) — change the shell working directory. Usage: cd [dir]"},
        {"pwd", "PWD(1) — print name of current/working directory."},
        {"cat", "CAT(1) — concatenate files and print on the standard output."},
        {"cp", "CP(1) — copy files and directories. Usage: cp [-r] SRC DEST"},
        {"mv", "MV(1) — move (rename) files. Usage: mv SRC DEST"},
        {"rm", "RM(1) — remove files or directories. Usage: rm [-r] FILE"},
        {"grep", "GREP(1) — print lines matching a pattern. Usage: grep [-i][-n][-v] PATTERN FILE"},
        {"ps", "PS(1) — report a snapshot of current processes."},
        {"df", "DF(1) — report file system disk space usage."},
        {"free", "FREE(1) — display amount of free and used memory."},
        {"top", "TOP(1) — display Linux processes (snapshot only here)."},
        {"touch", "TOUCH(1) — change file timestamps / create empty file."},
        {"head", "HEAD(1) — output the first part of files. Usage: head [-n N] FILE"},
        {"tail", "TAIL(1) — output the last part of files. Usage: tail [-n N] FILE"},
        {"find", "FIND(1) — search for files in a directory hierarchy."},
        {"wc", "WC(1) — print newline, word, and byte counts for files."},
        {"echo", "ECHO(1) — display a line of text."},
        {"date", "DATE(1) — print or set the system date and time."},
        {"alias", "ALIAS(1) — define or display aliases."},
        {"history", "HISTORY(1) — display command history."},
        {"whoami", "WHOAMI(1) — print effective user name."},
        {"env", "ENV(1) — run a program in a modified environment."},
        {"export", "EXPORT(1) — set export attribute for shell variables."},
        {"clear", "CLEAR(1) — clear the terminal screen."},
        {"help", "HELP(1) — show available local commands."}
    }

    Private Sub CmdMan(tokens As String())
        If tokens.Length < 2 Then
            RaiseOutputEvent("What manual page do you want?" & Environment.NewLine)
            ShowPrompt()
            Return
        End If

        If ManPages.ContainsKey(tokens(1)) Then
            RaiseOutputEvent(ManPages(tokens(1)) & Environment.NewLine)
        Else
            RaiseOutputEvent($"No manual entry for {tokens(1)}" & Environment.NewLine)
        End If
        ShowPrompt()
    End Sub

    Private Sub CmdTree(tokens As String())
        Dim root = _cwd
        If tokens.Length > 1 AndAlso tokens(1) <> "-" Then
            root = ResolvePath(tokens(1))
        End If

        If Not Directory.Exists(root) Then
            RaiseErrorEvent($"tree: {tokens(1)}: No such file or directory{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim sb As New StringBuilder()
        sb.AppendLine(root)
        Dim count As Integer = 0
        TreeVisit(root, "", sb, count)

        sb.AppendLine("")
        sb.AppendLine($"{count} directories, 0 files")
        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

    Private Sub TreeVisit(dir As String, prefix As String, sb As StringBuilder, ByRef dirCount As Integer)
        Dim entries As String()
        Try
            entries = Directory.GetFileSystemEntries(dir).OrderBy(Function(p) p, StringComparer.OrdinalIgnoreCase).ToArray()
        Catch
            Return
        End Try

        For i As Integer = 0 To entries.Length - 1
            Dim isLast = (i = entries.Length - 1)
            Dim name = Path.GetFileName(entries(i))
            Dim branch = If(isLast, "└── ", "├── ")
            sb.AppendLine(prefix & branch & name)

            If Directory.Exists(entries(i)) Then
                dirCount += 1
                Dim childPrefix = prefix & If(isLast, "    ", "│   ")
                TreeVisit(entries(i), childPrefix, sb, dirCount)
            End If
        Next
    End Sub

    Private Sub CmdLess(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"less: missing file operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If
        '  Pager not interactive here; just dump the file.
        CmdCat(tokens)
    End Sub

    Private Sub CmdSleep(tokens As String())
        If tokens.Length < 2 Then
            RaiseErrorEvent($"sleep: missing operand{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim secs As Double
        If Not Double.TryParse(tokens(1), secs) Then
            RaiseErrorEvent($"sleep: invalid time interval '{tokens(1)}'{Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Try
            Threading.Thread.Sleep(CInt(secs * 1000))
        Catch ex As Exception
            RaiseErrorEvent($"sleep: {ex.Message}{Environment.NewLine}")
        End Try

        ShowPrompt()
    End Sub

    Private Sub CmdCal(tokens As String())
        Dim year As Integer = DateTime.Now.Year
        Dim month As Integer = DateTime.Now.Month

        For i As Integer = 1 To tokens.Length - 1
            If Integer.TryParse(tokens(i), year) AndAlso year > 1000 AndAlso year < 10000 AndAlso (i = tokens.Length - 1 OrElse Not (tokens.Length > i + 1)) Then
                '  Could be a 4-digit year; if another numeric follows, it's month.
                If i + 1 < tokens.Length AndAlso Integer.TryParse(tokens(i + 1), month) AndAlso month >= 1 AndAlso month <= 12 Then
                    year = Integer.Parse(tokens(i))
                    month = Integer.Parse(tokens(i + 1))
                    Exit For
                End If
            ElseIf Integer.TryParse(tokens(i), month) AndAlso month >= 1 AndAlso month <= 12 Then
                month = Integer.Parse(tokens(i))
            End If
        Next

        Dim sb As New StringBuilder()
        Dim monthName = New Date(year, month, 1).ToString("MMMM yyyy")
        sb.AppendLine(" " & monthName.PadLeft(11 + monthName.Length \ 2))
        sb.AppendLine("Su Mo Tu We Th Fr Sa")

        Dim firstDay = New Date(year, month, 1).DayOfWeek
        Dim daysInMonth = Date.DaysInMonth(year, month)
        Dim offset = CInt(firstDay)
        Dim cell As Integer = 0

        For c As Integer = 0 To offset - 1
            sb.Append("   ")
            cell += 1
        Next

        For d As Integer = 1 To daysInMonth
            sb.Append(d.ToString().PadLeft(2) & " ")
            cell += 1
            If cell Mod 7 = 0 Then sb.AppendLine()
        Next
        sb.AppendLine()

        RaiseOutputEvent(sb.ToString())
        ShowPrompt()
    End Sub

#End Region

#Region "SSH command parsing"

    ''' <summary>
    ''' Parses <c>ssh [-p port] [-pw password] [-i keyfile] user@host</c> and fires
    ''' <see cref="SshConnectRequested"/>. On success the local session stops producing
    ''' further output; the host control switches to an SSH session. Invalid syntax
    ''' prints a usage hint and stays local.
    ''' </summary>
    Private Sub HandleSsh(tokens As String())
        '  ssh  →  usage
        If tokens.Length < 2 Then
            RaiseOutputEvent("usage: ssh [-p port] [-pw password] [-i keyfile] user@host" & Environment.NewLine)
            ShowPrompt()
            Return
        End If

        Dim port As Integer = 22
        Dim password As String = Nothing
        Dim keyFile As String = Nothing
        Dim userAtHost As String = Nothing
        Dim i As Integer = 1

        While i < tokens.Length
            If tokens(i) = "-p" Then
                If i + 1 >= tokens.Length Then
                    RaiseErrorEvent($"ssh: option requires an argument -- p{Environment.NewLine}")
                    ShowPrompt()
                    Return
                End If
                If Not Integer.TryParse(tokens(i + 1), port) OrElse port <= 0 OrElse port > 65535 Then
                    RaiseErrorEvent($"ssh: invalid port '{tokens(i + 1)}'{Environment.NewLine}")
                    ShowPrompt()
                    Return
                End If
                i += 2
            ElseIf tokens(i) = "-pw" Then
                If i + 1 >= tokens.Length Then
                    RaiseErrorEvent($"ssh: option requires an argument -- pw{Environment.NewLine}")
                    ShowPrompt()
                    Return
                End If
                password = tokens(i + 1)
                i += 2
            ElseIf tokens(i) = "-i" Then
                If i + 1 >= tokens.Length Then
                    RaiseErrorEvent($"ssh: option requires an argument -- i{Environment.NewLine}")
                    ShowPrompt()
                    Return
                End If
                keyFile = ResolvePath(tokens(i + 1))
                i += 2
            Else
                userAtHost = tokens(i)
                i += 1
            End If
        End While

        If String.IsNullOrEmpty(userAtHost) Then
            RaiseOutputEvent("usage: ssh [-p port] [-pw password] [-i keyfile] user@host" & Environment.NewLine)
            ShowPrompt()
            Return
        End If

        '  Split user@host.
        Dim parts As String() = userAtHost.Split({"@"c}, StringSplitOptions.None)
        If parts.Length <> 2 OrElse String.IsNullOrWhiteSpace(parts(0)) OrElse String.IsNullOrWhiteSpace(parts(1)) Then
            RaiseErrorEvent($"ssh: invalid target '{userAtHost}' (expected user@host){Environment.NewLine}")
            ShowPrompt()
            Return
        End If

        Dim username As String = parts(0).Trim()
        Dim hostname As String = parts(1).Trim()

        '  Stop local output; the host will switch to SSH.
        _running = False

        Dim options As New SshConnectionOptions() With {
            .Host = hostname,
            .Port = port,
            .UserName = username,
            .Password = If(password, ""),
            .PrivateKeyFile = If(keyFile, "")
        }

        RaiseEvent SshConnectRequested(options)
    End Sub

#End Region

#Region "Helpers"

    ''' <summary>Stores the raw input line for <c>echo</c> to reconstruct the original text.</summary>
    Private inputLine As String = ""

    ''' <summary>
    ''' Simple tokeniser that respects single- and double-quoted arguments
    ''' (bash-like).
    ''' </summary>
    Private Function ParseTokens(line As String) As String()
        inputLine = line
        Dim result As New List(Of String)()
        Dim i As Integer = 0

        While i < line.Length
            '  Skip whitespace.
            While i < line.Length AndAlso Char.IsWhiteSpace(line(i))
                i += 1
            End While
            If i >= line.Length Then Exit While

            If line(i) = """"c OrElse line(i) = "'"c Then
                Dim quote As Char = line(i)
                i += 1
                Dim start As Integer = i
                While i < line.Length AndAlso line(i) <> quote
                    i += 1
                End While
                result.Add(line.Substring(start, i - start))
                If i < line.Length Then i += 1 ' skip closing quote
            Else
                Dim start As Integer = i
                While i < line.Length AndAlso Not Char.IsWhiteSpace(line(i))
                    i += 1
                End While
                result.Add(line.Substring(start, i - start))
            End If
        End While

        Return result.ToArray()
    End Function

    ''' <summary>
    ''' Resolves a (possibly relative) path against <see cref="_cwd"/>.
    ''' Supports <c>~</c> for the user profile directory.
    ''' </summary>
    Private Function ResolvePath(path As String) As String
        If String.IsNullOrEmpty(path) Then Return _cwd

        If path.StartsWith("~") Then
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) & path.Substring(1)
        End If

        If System.IO.Path.IsPathRooted(path) Then
            Return path
        End If

        Return path.GetFullPath(System.IO.Path.Combine(_cwd, path))
    End Function

    ''' <summary>
    ''' Prints the ANSI-coloured prompt: <c>user@machine:/cwd$ </c>.
    ''' The user/host part is green; the working-directory part is blue.
    ''' </summary>
    Private Sub ShowPrompt()
        Dim displayPath As String = _cwd

        '  Shorten the home directory to ~ for a familiar bash look.
        Dim home As String = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        If displayPath.StartsWith(home, StringComparison.OrdinalIgnoreCase) Then
            displayPath = "~" & displayPath.Substring(home.Length)
        End If

        '  Build:  green(user@machine) reset : blue(cwd) reset $
        Dim promptText As String =
            New TextSpan(Environment.UserName & "@" & Environment.MachineName, AnsiColor.Green) &
            AnsiEscapeCodes.Reset & " " &
            New TextSpan(displayPath, AnsiColor.Cyan) &
            AnsiEscapeCodes.Reset & vbCrLf & "$ "

        RaiseOutputEvent(promptText)
    End Sub

#End Region

End Class
