---
name: console-winform-ui-fix
overview: 修复 WinForm 控制台控件的两个 UI 交互问题：1) 命令输出从光标位置插入而非追加到末尾；2) 等待输入时光标在别处时无法输入。两项均因 RichTextBox 的选中/光标位置被误用所致，需让输出固定追加到末尾、让输入按键自动把光标移回输入点。
todos:
  - id: fix-output-append
    content: 修改 WriteOutput 与 WriteInput 回显为末尾追加并 ScrollToCaret
    status: completed
  - id: fix-keydown-caret
    content: 在 richTextBoxConsole_KeyDown 增加输入按键光标归位逻辑
    status: completed
    dependencies:
      - fix-output-append
  - id: verify-build
    content: 构建项目并验证输出/输入交互行为符合原生 console
    status: completed
    dependencies:
      - fix-keydown-caret
---

## 用户需求

对当前模拟命令行终端的 WinForm 控件项目进行代码审查，定位并修复两处与原生 console 行为不一致的 UI 交互问题。

## 核心问题

- 问题 1（输出错位）：命令输出时若光标位于 RichTextBox 任意位置，下游输出会从光标位置插入，而非追加到内容末尾。
- 问题 2（无法输入）：等待用户输入时若光标不在输入位置，按键被吞掉，无法输入；原生 console 应把光标移回输入点继续输入。

## 预期修复效果

- 所有命令输出/回显始终追加在控件内容末尾，不受光标位置影响。
- 等待输入时，无论光标在何处，用户敲击字符都会自动把光标移回当前输入行末尾并正常输入，方向键与 Ctrl-C 在历史区仍可用，且历史输出不会被改写。

## 技术栈

- 语言/框架：VB.NET + Windows Forms（.NET 5，项目文件 console.NET5.vbproj）
- 目标控件：RichTextBox（字段名 richTextBoxConsole）
- 关键状态：inputStart（输入行起始索引）、m_isInputEnabled（输入开关）

## 实现方案

### 总体策略

问题根因是 RichTextBox 的"选中/光标位置"被当成写入锚点。修复分为两处：

1. 输出写入：强制把锚点移到内容末尾后再追加，使输出不依赖光标位置。
2. 输入交互：在 KeyDown 阶段对"输入类按键"自动把光标归位到输入行末尾，模拟原生 console 单光标行为，并避免破坏历史区只读保护。

### 修复 A：输出与回显固定追加到末尾（解决问题 1）

修改 `WinForm/ConsoleControl.vb`：

- `WriteOutput` 的 Invoke 内（原第 356-361 行）改为：先将 `SelectionStart` 设为 `TextLength`、`SelectionLength` 设为 0，再设置 `SelectionColor` 并调用 `AppendText(output)`，随后 `inputStart = richTextBoxConsole.TextLength`，末尾 `ScrollToCaret()` 保持视图在底部。
- `WriteInput` 回显分支（原第 385-389 行，echo=True 时）采用完全相同的逻辑：移到末尾 → `AppendText` → 更新 `inputStart` → `ScrollToCaret()`。

原理：`AppendText` 始终在末尾追加，且不受当前光标影响；`SelectedText &= ...` 之所以出错，是因为它作用于当前选中/光标位置。显式重置选区后再 `AppendText` 可彻底消除插入位置歧义。

### 修复 B：输入按键自动归位（解决问题 2）

在 `richTextBoxConsole_KeyDown`（原第 284-341 行）中，于既有"只读区判定"之前插入归位逻辑，仅当 `m_isInputEnabled AndAlso inputStart >= 0` 时生效：

- 计算 `inputEnd = richTextBoxConsole.TextLength`（当前输入行末尾，新字符应插入处）。
- 判定"输入类按键"：排除方向键（Left/Right/Up/Down）与 Ctrl-C；Backspace 仅在 `inputEnd > inputStart` 时视为输入类（避免无输入时越界删除历史）。
- 若 `SelectionStart <> inputEnd`，在 KeyDown 阶段将其设回 `inputEnd`、`SelectionLength = 0` 并 `ScrollToCaret()`。由于 KeyDown 先于文本插入触发，后续字符会正确插入到输入行末尾。
- 归位后，原第 286 行 `isInReadOnlyZone = SelectionStart < inputStart` 对输入类按键变为 False，原第 320-330 行的 `SuppressKeyPress` 不再触发 → 解决"无法输入"。
- 方向键/Ctrl-C 不归位，保留在历史区的浏览与复制能力；第 315 行 Backspace 抑制（SelectionStart <= inputStart）在归位后为 False，且仅删除最后输入字符；第 333-340 行 Return 取 `inputStart` 到 `SelectionStart(inputEnd)` 的输入文本仍正确。

### 性能与可靠性

- 改动集中在单一文件、单一控件，无新增类型或依赖，回归面小。
- 归位赋值为 O(1) 字段操作，无循环/遍历；`AppendText` 为 WinForms 原生高效追加。
- 不改动 `AnsiEscapeRenderer.vb`（其 `AppendText` 追加逻辑本就正确）、`Win32/ProcessInterface.vb` 等无关模块，保持向后兼容。
- 护眼/无闪烁：复用现有 Invoke 跨线程写控件模式，追加逻辑不改变重绘频率。

## 目录结构与改动文件

仅修改一个文件即可覆盖两个问题的修复：

```
g:/mini-R/src/console/console/WinForm/
└── ConsoleControl.vb   # [MODIFY] 修复 WriteOutput/WriteInput 回显改为末尾追加；
                        #           richTextBoxConsole_KeyDown 增加输入按键光标归位逻辑。
```

其余文件（AnsiEscapeRenderer.vb、Console.vb、Win32/* 等）经审查无需改动。

## 关键实现要点（文本说明，非完整代码）

- 写入锚点统一为 `richTextBoxConsole.TextLength`，写入方式统一为 `AppendText`。
- `inputStart` 在每次输出/回显后更新为 `TextLength`，维持"输入区 = [inputStart, TextLength)" 不变式。
- KeyDown 中"归位"仅针对输入类按键，且以 `m_isInputEnabled AndAlso inputStart >= 0` 为前置条件，不影响只读态与初始无提示态。