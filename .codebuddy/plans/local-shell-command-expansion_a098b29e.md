---
name: local-shell-command-expansion
overview: 在 SShClient\LocalShellInterface.vb 中大幅扩展模拟的 Linux 命令集，新增文件/文本、系统信息、Shell 内建、常用工具四类命令；引入会话状态（环境变量、别名、命令历史）以实现高度还原的 bash 体验。
todos:
  - id: add-session-state
    content: 新增 _env/_aliases/_history 状态字段并在 StartProcess 初始化
    status: completed
  - id: alias-expand
    content: 实现 ExpandAlias 并在 WriteInput 分发前展开别名
    status: completed
    dependencies:
      - add-session-state
  - id: file-text-cmds
    content: 实现文件/文本类命令 cp/mv/touch/head/tail/grep/wc/find/file/ln/sort/uniq/cut/tr/tee/sed/awk
    status: completed
    dependencies:
      - alias-expand
  - id: sysinfo-cmds
    content: 实现系统信息类命令 date/uname/hostname/env/export/id/uptime/ps/free/df/top/who
    status: completed
    dependencies:
      - alias-expand
  - id: builtin-cmds
    content: 实现 Shell 内建 alias/unalias/history/which/type/set/unset/source
    status: completed
    dependencies:
      - alias-expand
  - id: util-cmds
    content: 实现工具命令 man/tree/less/more/sleep/cal
    status: completed
    dependencies:
      - alias-expand
  - id: update-meta
    content: 更新 BuiltinCommands 数组、CmdHelp 帮助文本与分区 Region 注释
    status: completed
    dependencies:
      - file-text-cmds
      - sysinfo-cmds
      - builtin-cmds
      - util-cmds
---

## 用户需求

优化 `SShClient\LocalShellInterface.vb` 模块，在现有 12 个命令基础上，新增大量基础 bash/linux 命令模拟，构建一个较完善的本地命令行环境。

## 产品概述

在现有不依赖外部进程的本地 Shell 后端中，扩充命令集并引入会话级状态（环境变量、别名、命令历史），使模拟的 Linux 命令行在命令覆盖、输出格式和选项行为上高度贴近真实 bash，同时保持现有 ssh 拦截、Tab 补全、Ctrl+C/Ctrl+L 等交互行为不变。

## 核心功能

- 新增文件/文本类命令：cp、mv、touch、head、tail、grep、wc、find、file、ln、sort、uniq、cut、tr、tee、sed（基础）、awk（基础）
- 新增系统信息类命令：date、uname、hostname、env、export、id、uptime、ps、free、df、top（精简）、who
- 新增 Shell 内建命令：alias、unalias、history、which、type、set、unset、source（. 点号）
- 新增其他常用工具：man、tree、less/more、sleep、cal
- 引入会话状态：环境变量（HOME/USER/PATH/PS1 等）、命令别名、命令历史；输入行在分发前先展开别名
- 命令支持常用选项（如 ls -la、ps aux、df -h），输出格式贴近真实 bash
- 同步更新 Tab 补全命令表、help 帮助文本，保持现有 ssh 拦截与控制台交互逻辑不变

## 技术栈

- 语言/平台：VB.NET（.NET Framework / .NET），与现有项目一致
- 基础类型复用：Microsoft.VisualBasic 的 `TextSpan`、`AnsiColor`、`AnsiEscapeCodes`、`ConsoleInputMode`、`IEditableInputLine`
- 基类方法复用：`AbstractProcessInterface` 的 `RaiseOutputEvent`、`RaiseErrorEvent`、`RaiseSetInputLineEvent`、`RaiseExitEvent`、`ansi`
- 文件系统：`System.IO`（DirectoryInfo/FileInfo/File/Directory）、`Environment`
- 局部进程信息：通过 `System.Diagnostics.Process.GetProcesses()` 模拟 `ps`，通过 `PerformanceCounter` 或 `Process` 工作集模拟 `free`/`top`

## 实现方案

在单文件 `LocalShellInterface.vb` 内，沿用现有「`WriteInput` → `Select Case cmd` → `CmdXxx(tokens)`」分发架构，扩展命令表与命令实现；新增私有字段维护会话状态，并在命令分发链前插入别名展开步骤。所有新增命令遵循现有约定：文件系统类复用 `ResolvePath`（已支持 ~ 与相对路径），需要保留原始参数的命令（grep/sed/cut/tr/awk 等）复用 `inputLine` 字段而非仅依赖 `tokens`。

关键决策：

1. 会话状态 `_env`、`_aliases`、`_history` 作为私有字段，在 `StartProcess` 初始化，避免影响其他模块与全局状态。
2. 别名展开在 `WriteInput` 解析 tokens 之后、Select 分发之前完成：对首 token 查 `_aliases`，命中则整体替换命令文本并重新 `ParseTokens`，保持单次展开（不递归，避免死循环）。
3. 命令实现分组为 Region：File/Text 命令、System Info 命令、Builtin 命令、Utility 命令，与现有 `#Region "Command implementations"` 风格一致。
4. `grep`/`sed`/`awk`/`cut`/`tr` 等以「实用子集」实现（正则、基础替换、字段切分、字符转换），不做完整 POSIX 语法解析。
5. `ps`/`free`/`top` 在 Windows 宿主上以 `Process.GetProcesses()` 与进程内存近似模拟 Linux 字段（PID、USER 用当前用户、COMMAND 用进程名、%MEM 用工作集占比），并在 help 中注明为模拟值。
6. 性能：所有枚举限定单目录层（参考现有 `CompletePath` 的 maxCandidates 上限），`find` 递归采用受限深度/上限保护 UI 线程。

## 实现要点（防回归）

- 保持 `ssh` 命令拦截与 `SshConnectRequested` 事件逻辑完全不变。
- 更新 `BuiltinCommands` 数组，确保 Tab 补全可发现所有新增命令。
- 更新 `CmdHelp` 输出，分类列出新增命令及用法。
- 保持 `PreferredInputMode = LineEdit`、Ctrl+C/Ctrl+L 行为、`echo` 对 `inputLine` 的依赖不变。
- 命令历史 `history` 显示带序号，支持 `!<n>` 调用（可选）；新命令入历史前做去重/空行过滤。
- 错误路径统一用 `RaiseErrorEvent` + `ShowPrompt()`，沿用现有 catch-all 保护会话存活。

## 架构设计

单一类内部分层，不引入新类或新文件：

- 状态层：`_cwd`、`_env`、`_aliases`、`_history`、`inputLine`（已存在）、`_editorLine`（已存在）
- 解析层：`ParseTokens`、`ResolvePath`、新增 `ExpandAlias`
- 分发层：`WriteInput`（含别名展开）、`WriteRaw`（已存在）
- 命令层：按类别分组 `CmdXxx`
- 输出层：`ShowPrompt`、`RaiseOutputEvent` 等（复用基类/现有）

无需 Mermaid 图（单文件内部扩展，结构清晰）。

## 目录结构

```
g:/mini-R/src/console/SShClient/
└── LocalShellInterface.vb   # [MODIFY] 唯一修改文件。新增四类命令实现与分组 Region；新增 _env/_aliases/_history 状态字段并在 StartProcess 初始化；新增 ExpandAlias 别名展开；更新 BuiltinCommands 数组（Tab 补全）、CmdHelp（帮助文本）；保持 ssh 拦截、WriteRaw、ShowPrompt、echo 等现有逻辑不变。
```

## 关键代码结构（接口级）

新增私有状态字段（示意，非实现体）：

- `_env As Dictionary(Of String, String)`：会话环境变量，初始含 HOME/USER/HOSTNAME/PATH/PS1/LANG。
- `_aliases As Dictionary(Of String, String)`：命令别名表。
- `_history As List(Of String)`：命令历史（带序号展示）。
- `Private Function ExpandAlias(line As String) As String`：对首 token 查别名并替换，返回展开后命令文本。