---
name: 修复ConsoleControl的ANSI渲染
overview: 修复 WinForm ConsoleControl 的 WriteAnsiEscape 渲染问题：1) ANSI 输出因 args.Ansi 永远为 False 而不走渲染路径（无颜色/样式）；2) 裸 \r 的 CarriageReturn 删除整行导致 ubuntu SSH 文本显示为空白行。
todos:
  - id: fix-ansi-flag
    content: 修改 AbstractProcessInterface，输出事件携带 ansi 标志并暴露可配置属性
    status: pending
  - id: fix-carriage-return
    content: 修正 AnsiEscapeRenderer 的 CarriageReturn 仅移动插入点到行首
    status: pending
  - id: add-ansi-fallback
    content: ConsoleControl 输出分发增加含 ESC 字符降级走 WriteAnsiEscape
    status: pending
    dependencies:
      - fix-ansi-flag
  - id: verify-test-string
    content: 用 ubuntu ls -l 测试字符串验证样式与文本完整显示
    status: pending
    dependencies:
      - fix-carriage-return
      - add-ansi-fallback
---

## 用户需求

修复 WinForm ConsoleControl 控件的 `WriteAnsiEscape` ANSI 渲染函数，使其能正确渲染包含 ANSI escape sequence 的文本。

## 产品概述

当前项目为 vb.net 编写的 WinForms 控制台模拟控件库。其 ANSI 渲染存在两类缺陷，需要修复以正确显示 ubuntu SSH 等带有 ANSI 转义码的输出。

## 核心功能

- 含 ANSI escape 的字符串（颜色、背景色、粗体、斜体、下划线等）应被正确解析并以对应样式渲染到 RichTextBox，而非原样显示转义字符。
- ubuntu SSH 输出的 ANSI 文本（如 `ls -l` 结果中蓝色 `snap`、命令行提示符等）应完整显示，不再变成空白行。
- 维护跨调用延续的终端格式状态，支持被分片到达、尚未结束的转义序列拼接缓冲（SSH 分包场景）。

## 技术栈

- 语言/框架：VB.NET + .NET 5 + WinForms（现有 `console.NET5.vbproj` 控件库）
- UI 控件：System.Windows.Forms.RichTextBox（用作控制台文本输出区）
- 渲染解析：现有 `AnsiEscapeRenderer` 静态类，解析 xterm 子集 CSI/SGR/OSC

## 实现方案

### 总体策略

修复两处根因，使 ANSI 文本既能进入渲染路径（问题1），又能正确保留文本内容（问题2），同时保持现有分片缓冲与状态延续逻辑不变。

### 根因与修复

**根因A（问题1：无样式）—— ANSI 路径从未被触发**

- `AbstractProcessInterface.vb` 第52行 `Protected ansi As Boolean = False`，且全项目后端从未将其设为 True。
- 第131-137行 `outputWorker_ProgressChanged` 与第165-171行 `errorWorker_ProgressChanged` 使用单参构造 `New ProcessEventArgs(text)`，导致 `args.Ansi` 恒为 False。
- `ConsoleControl.vb` 第264行 `If args.Ansi Then WriteAnsiEscape(...)` 因此永远走 `WriteOutput(..., Color.White)` 纯文本路径，转义字符被原样追加。
- **修复**：`outputWorker_ProgressChanged` / `errorWorker_ProgressChanged` 改用已有的 `RaiseOutputEvent(text)` / `RaiseErrorEvent(text)`（其内部已构造带 `ansi` 标志的 `ProcessEventArgs`），使 `ansi` 字段生效；并将 `ansi` 字段暴露为可配置 `Property`，由后端决定是否启用 ANSI 输出（如 SSH 会话或启用虚拟终端的进程设为 True，本地 cmd.exe 默认保持 False）。

**根因B（问题2：ubuntu 空白行）—— `\r` 删除整行**

- `AnsiEscapeRenderer.vb` 第330-342行 `CarriageReturn`：遇到裸 `\r` 时执行 `rtb.Select(lineStart, lineEnd-lineStart); rtb.SelectedText=""`，即清空整行。
- 测试字符串中 `\r`（`vbCr`）出现在已写入文本行尾（"ls -l" 行尾、"xieguigang@apache-php:~$ " 行尾），导致刚写入的整行被删空 → 显示为空白行。
- **修复**：裸 `\r` 的正确终端语义是仅把插入点移回当前行首、不删除任何文本。将 `CarriageReturn` 改为只设置 `rtb.SelectionStart = lineStart; rtb.SelectionLength = 0`，移除"清除整行"逻辑。后续文本从行首覆盖写入时，RichTextBox 的 `AppendText` 在行尾追加即可（行首已有内容保留，符合终端换行后重绘的多数场景；进度条等 `\r` 重绘覆盖场景因无后续覆盖文本，内容得以保留，避免丢字）。

**降级保护（可选但推荐）**

- `ConsoleControl.vb` 第262-272行：当 `args.Ansi = False` 但 `args.Content` 包含 ESC 字符（`ChrW(&H1B)`）时，也调用 `WriteAnsiEscape`，避免本地进程未声明 ANSI 却输出了转义码时漏渲染。

## 实现注意

- 仅修改解析/分发逻辑，不改动 SGR 颜色映射、`Xterm256Color`、`AppendStyled`、`IsInsideUnterminatedEscape` 等已验证正确的部分。
- 性能：`RaiseOutputEvent` 复用现有事件构造，无额外开销；`CarriageReturn` 改为仅设 SelectionStart，去除整行删除带来的文本重排开销。
- 向后兼容：`ansi` 默认仍为 False，本地 cmd.exe 行为不变；仅当后端显式启用时才走 ANSI 渲染。
- 无新增依赖、无新增文件，全部为就地修改。

## 架构设计

数据流（修复后）：

```mermaid
flowchart LR
    A[进程/SSH 后端输出] --> B[AbstractProcessInterface.outputWorker_ProgressChanged]
    B -->|改用 RaiseOutputEvent 携带 ansi 标志| C[OnProcessOutput 事件]
    C -->|args.Ansi=True 或 含ESC字符| D[WriteAnsiEscape]
    D --> E[AnsiEscapeRenderer.Render]
    E -->|解析 CSI/SGR/OSC, 修正 CarriageReturn| F[RichTextBox 样式化文本]
```

## 目录结构

```
g:/mini-R/src/console/console/
├── Win32/
│   └── AbstractProcessInterface.vb   # [MODIFY] outputWorker/errorWorker_ProgressChanged 改为调用 RaiseOutputEvent/RaiseErrorEvent；将 ansi 字段改为 Public Property 供后端配置
├── WinForm/
│   └── ConsoleControl.vb             # [MODIFY] processInterace_OnProcessOutput 增加"含ESC则降级走 WriteAnsiEscape"逻辑
└── AnsiEscapeRenderer.vb             # [MODIFY] CarriageReturn 仅移动插入点到行首，删除整行清除逻辑
```

## 关键代码结构

修改点示意（VB.NET 片段，仅展示接口级变更）：

```
' AbstractProcessInterface.vb
Public Property EnableAnsi As Boolean
    Get
        Return ansi
    End Get
    Set(value As Boolean)
        ansi = value
    End Set
End Property

' outputWorker_ProgressChanged / errorWorker_ProgressChanged 中
If TypeOf e.UserState Is String Then
    RaiseOutputEvent(TryCast(e.UserState, String))
End If
```