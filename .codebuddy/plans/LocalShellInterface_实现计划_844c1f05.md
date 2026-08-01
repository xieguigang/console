---
name: LocalShellInterface 实现计划
overview: 实现一个不启动外部进程的本地类 bash 解释器 LocalShellInterface，支持基础文件系统命令与 ssh 命令解析，并通过事件通知 SshWinFormConsole 切换 ssh 会话，ssh 退出后重建本地会话。
todos:
  - id: impl-localshell
    content: 实现 LocalShellInterface 的命令解析、执行与 ANSI 提示符输出
    status: completed
  - id: impl-ssh-event
    content: 在 LocalShellInterface 新增 SshConnectRequested 事件并解析 ssh 参数
    status: completed
    dependencies:
      - impl-localshell
  - id: wire-host
    content: 在 SshWinFormConsole 订阅事件并切换到 SSH 会话
    status: completed
    dependencies:
      - impl-ssh-event
  - id: verify-reset
    content: 验证 SSH 断开后 ResetToLocalShell 重建本地会话
    status: completed
    dependencies:
      - wire-host
---

## 用户需求

在现有 VB.NET console 终端模拟 WinForm 控件模块中，实现一个基础的本地 shell 模块 `SShClient/LocalShellInterface.vb`，默认加载在 `SshWinFormConsole` 之中。当 `SshWinFormConsole` 尚未建立 SSH 连接时，用户可在本地执行基础命令；当输入 `ssh [-p port] username@host` 时，解析连接参数并切换到 SSH 会话；SSH 会话结束时自动重建本地会话。

## 产品概述

`LocalShellInterface` 是一个不依赖任何外部进程的"类 bash"解释器后端，继承自 `AbstractProcessInterface`。它自行解析用户输入的命令并通过 `RaiseOutputEvent` 将结果以 ANSI 着色文本输出到 `ConsoleControl`。当识别到 ssh 连接命令时，通过自定义事件把解析出的连接参数交给宿主 `SshWinFormConsole` 完成后端切换；SSH 断开后 `SshWinFormConsole` 负责重建一个新的 `LocalShellInterface` 实例。

## 核心功能

- 维护本地会话运行状态与当前工作目录（初始为 `Environment.CurrentDirectory`）。
- 提供带 ANSI 着色的命令提示符：`user@machine:/cwd$ `。
- 支持本地基础文件系统命令：`pwd`、`ls`/`ls -l`、`cd <dir>`、`cat <file>`、`echo <text>`、`mkdir <dir>`、`rm <file|dir>`、`clear`、`whoami`、`help`、`exit`、未知命令提示 `command not found`。
- 解析 `ssh [-p port] user@host`：提取主机、端口、用户名，通过事件将 `SshConnectionOptions` 交给宿主建立 SSH 会话，本地会话结束（不再输出提示符）。
- SSH 断开后，由 `SshWinFormConsole` 重建 `LocalShellInterface` 并回到本地会话。

## 技术栈选择

- 语言：VB.NET（WinForms），沿用现有项目技术栈。
- 基类：`Win32.AbstractProcessInterface`（与 `SshProcessInterface`、`ProcessInterface` 一致）。
- 输出渲染：复用现有 `RaiseOutputEvent`/`RaiseErrorEvent`，内容使用 `Console.TextSpan` + `Console.AnsiColor` + `Console.AnsiEscapeCodes` 拼接 ANSI 字符串，以 `ansi:=True` 提交。
- 清屏：使用 ANSI 清屏转义码 `\x1b[2J\x1b[H`，避免直接依赖宿主控件（保持后端独立）。

## 实现方案

### 总体策略

在 `LocalShellInterface` 中实现一套纯内存的命令解释循环：每次 `WriteInput(input)` 被 `ConsoleControl` 调用时，先输出换行，再解析命令；若为 ssh 则触发自定义事件并停止本地输出；否则执行本地命令并把结果 + 新提示符通过 `RaiseOutputEvent` 回写。会话生命周期由基类约定的 `StartProcess`/`StopProcess` 与 `IsProcessRunning` 控制（`RaiseOutputEvent` 内部已门控于 `IsProcessRunning`）。

### 关键技术决策

1. **不启动进程**：与用户澄清一致，`LocalShellInterface` 自身解释命令，仅使用 `System.IO` 做真实文件操作，输出即时且零进程开销。
2. **自定义事件 `SshConnectRequested(options As SshConnectionOptions)`**：本地 shell 不持有宿主引用，通过事件解耦；`SshWinFormConsole` 订阅该事件后调用既有 `Connect(options)` 完成切换，符合用户选择的解耦机制。
3. **提示符自绘**：`ConsoleControl` 不会自动打印提示符，因此每次本地命令执行完毕后由 `LocalShellInterface` 在输出末尾追加提示符，保证交互连续。
4. **SSH 解析复用 `SshConnectionOptions`**：直接复用项目中已有的连接参数类型，避免新增数据结构，与 `SshProcessInterface` 接入点一致。
5. **会话切换回退**：`SshWinFormConsole.ResetToLocalShell()` 已能重建 `LocalShellInterface` 并 `StartProcess`，无需改动即可覆盖"SSH 断开回到本地"的需求。

### 性能与可靠性

- 命令执行均为本地同步 I/O，单次解析/执行复杂度 O(n)（n 为输入长度），开销可忽略；文件列举 `Directory.GetFiles/GetDirectories` 为 O(k)，k 为目录条目数，对常规目录无瓶颈。
- 所有 `RaiseOutputEvent` 调用均在 `IsProcessRunning = True` 后进行；`StopProcess` 置 `_running = False` 可即时阻止后续输出，避免 SSH 切换期间的竞态。
- 异常路径（如目录不存在、权限不足）通过 `RaiseErrorEvent` 以 bash 风格红字提示，不抛出到宿主线程。

## 实现注意事项

- `WriteInput` 开头先 `RaiseOutputEvent(New ProcessEventArgs(vbCrLf, ansi:=False))`，使本地输出从新行开始（用户输入回车后光标停在输入行末）。
- `cd` 调用 `Directory.SetCurrentDirectory` 并同步 `_cwd`；非法路径给出错误提示且不改动 `_cwd`。
- `clear` 输出 ANSI 清屏码而非调用 `ConsoleControl.ClearOutput`，保持后端无宿主耦合。
- ssh 命令解析：支持可选 `-p <port>`（默认 22），其余 token 须为 `user@host`；非法格式输出用法提示并停留在本地会话。
- `WriteRaw` 保持空实现（本地无子进程，控制字符无意义），与 `SshProcessInterface` 的语义区分清晰。

## 架构设计

### 组件关系

```mermaid
graph TD
    A[SshWinFormConsole] -->|SetConsoleCore| B(LocalShellInterface)
    A -->|SetConsoleCore| C(SshProcessInterface)
    B -->|SshConnectRequested(options)| A
    A -->|Connect| C
    C -->|ProcessExited| A
    A -->|ResetToLocalShell| B
    B -.继承自.-> D[AbstractProcessInterface]
    C -.继承自.-> D
```

本地会话与 SSH 会话共用 `ConsoleControl` 的渲染与输入管线，通过 `SetConsoleCore` 切换后端；`LocalShellInterface` 仅通过事件通知宿主，不直接操控 SSH。

## 目录结构

```
SShClient/
└── LocalShellInterface.vb   # [MODIFY] 实现本地类 bash 解释器后端。新增 _running、_cwd 字段；实现 IsProcessRunning、StartProcess、StopProcess、WriteInput、WriteRaw；新增 SshConnectRequested 事件与命令解析/执行逻辑（pwd/ls/cd/cat/echo/mkdir/rm/clear/whoami/help/exit/ssh）；输出 ANSI 着色提示符与结果。
SShClient/
└── SshWinFormConsole.vb     # [MODIFY] 在构造或 Load 中订阅 m_localInterface.SshConnectRequested 事件；事件处理程序从 options 构造 SshConnectionOptions 并调用 Connect(options) 完成会话切换。ResetToLocalShell 已支持重建本地会话，无需改动。
```

## 关键代码结构

```
' LocalShellInterface.vb 新增事件（供宿主订阅）
Public Event SshConnectRequested(options As SshConnectionOptions)

' IsProcessRunning 必须实现（基类抽象）
Public Overrides ReadOnly Property IsProcessRunning As Boolean
    Get
        Return _running
    End Get
End Property

' WriteInput 主体签名（基类 MustOverride）
Public Overrides Sub WriteInput(input As String)
```

## Agent Extensions

### SubAgent

- **code-explorer**
- 用途：在生成实现细节前，对 `ConsoleControl` 输入回显时序、`ProcessEventArgs` 构造约定、`SshConnectionOptions` 字段做最终交叉验证，确保事件参数与输出 API 调用精确无误。
- 预期结果：确认 `WriteInput` 调用链、`RaiseOutputEvent` 门控条件、ssh 参数映射字段，避免实现阶段出现 API 误用。