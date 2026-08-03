Imports System.IO
Imports System.Text
Imports System.Text.RegularExpressions
Imports Microsoft.VisualBasic.ApplicationServices.Terminal
Imports Microsoft.VisualBasic.Windows.Forms.Win32

''' <summary>
''' A built-in local shell backend that simulates a bash-like environment without
''' launching an external process. It intercepts the <c>ssh</c> command and raises
''' <see cref="SshConnectRequested"/> so the host control can switch to a real SSH
''' session. All other commands (pwd, ls, cd, cat, echo, mkdir, rm, clear, whoami,
''' help, exit) are executed directly using .NET System.IO.
''' </summary>
Public Class LocalShellInterface : Inherits AbstractProcessInterface : Implements IEditableInputLine

    Private _running As Boolean = False
    Private _cwd As String = Environment.CurrentDirectory

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
        ShowPrompt()
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

        Dim tokens As String() = ParseTokens(line)
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
        "cat", "cd", "clear", "echo", "exit", "help",
        "ls", "mkdir", "pwd", "rm", "ssh", "whoami"
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
        sb.AppendLine("  pwd                     Print working directory")
        sb.AppendLine("  ls [-l] [path]          List directory contents")
        sb.AppendLine("  cd [dir]                Change directory (default: home)")
        sb.AppendLine("  cat <file>              Print file contents")
        sb.AppendLine("  echo <text>             Print text to output")
        sb.AppendLine("  mkdir <dir>             Create a directory")
        sb.AppendLine("  rm <path>               Remove file or directory (recursive)")
        sb.AppendLine("  clear                   Clear the screen")
        sb.AppendLine("  whoami                  Print current user name")
        sb.AppendLine("  help                    Show this help")
        sb.AppendLine("  exit                    End the local session")
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
