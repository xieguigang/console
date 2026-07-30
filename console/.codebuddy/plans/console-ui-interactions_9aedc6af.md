---
name: console-ui-interactions
overview: 为 WinForm 控制台模拟控件（ConsoleControl.vb）新增三项鼠标/键盘交互：鼠标选择文本自动复制到剪贴板、右键将剪贴板文本粘贴进输入缓冲、上下方向键浏览输入历史并替换当前输入缓冲。所有改动集中在 WinForm/ConsoleControl.vb 一个文件中。
todos:
  - id: add-fields-and-init
    content: 新增 inputHistory/historyIndex 字段，并在 New() 中设置 ContextMenuStrip 抑制原生右键菜单
    status: completed
  - id: implement-mouse-handler
    content: 实现 MouseUp 处理（左键选择复制、右键粘贴）及 InsertIntoInputBuffer 辅助方法
    status: completed
  - id: implement-history-nav
    content: 在 KeyDown 中加入上下方向键历史导航与 ReplaceInputBuffer，并在 Enter 提交时记录历史
    status: completed
    dependencies:
      - add-fields-and-init
---

## 用户需求

为 VB.NET 编写的 WinForm 控制台模拟控件（WinForm/ConsoleControl.vb，内部用 RichTextBox 模拟终端窗口）新增以下 UI 交互功能：

## 核心功能

- 鼠标选择复制：用户在 RichTextBox 上用鼠标选中一段文本后，自动将选中文本复制到系统剪贴板（无需按回车或 Ctrl+C）。
- 右键粘贴：用户在 RichTextBox 上点击鼠标右键后，若系统剪贴板中存在文本，则将该文本插入到当前输入缓冲（插入位置为光标处，若光标位于只读历史区则插入到当前输入行末尾）。
- 上下方向键历史回放：用户在 RichTextBox 聚焦时按上/下方向键，将上一条/下一条历史输入替换当前输入缓冲区；到达最旧历史后再按“下”则清空输入行为空；若光标位于只读历史区（SelectionStart < inputStart）则按普通滚动导航处理，不触发历史。

## 补充说明

- 保留 RichTextBox 原生 Ctrl+C/Ctrl+V 等快捷键行为；仅屏蔽其自带右键菜单（避免与右键粘贴冲突）。
- 输入历史在用户按回车提交非空输入时记录，空输入不计入历史。

## 技术栈

- 语言/框架：VB.NET + Windows Forms（.NET 5，项目 console.NET5.vbproj）
- 核心控件：System.Windows.Forms.RichTextBox（作为终端显示与输入区）
- 现有约定：事件统一使用 `Handles richTextBoxConsole.Xxx` 绑定；当前输入缓冲 = RichTextBox 文本中从 `inputStart` 到末尾的子串。

## 实现方案

完全在现有 `WinForm/ConsoleControl.vb` 单文件内扩展，遵循既有 `WithEvents` + `Handles` 模式，不改动设计器文件（右键菜单抑制放在 `New()` 中代码设置，避免触碰 `InitializeComponent`）。

1. **状态字段**：新增 `inputHistory As New List(Of String)` 与 `historyIndex As Integer = 0`（0..Count，等于 Count 表示“当前空行/进行中行”）。
2. **屏蔽原生右键菜单**：在 `New()` 的 `InitializeComponent()` 之后设置 `richTextBoxConsole.ContextMenuStrip = New ContextMenuStrip()`，抑制 RichTextBox 默认右键菜单，同时保留原生 Ctrl+C/Ctrl+V 快捷键。
3. **鼠标交互**：新增 `richTextBoxConsole_MouseUp` 处理程序（Handles MouseUp）：

- 左键且 `SelectionLength > 0` → 调用 `richTextBoxConsole.Copy()` 实现“选择即复制”。
- 右键且 `Clipboard.ContainsText()` → 读取 `Clipboard.GetText()` 并调用 `InsertIntoInputBuffer` 插入输入缓冲。

4. **辅助方法**：`InsertIntoInputBuffer(text)`（在输入区/光标处插入文本）与 `ReplaceInputBuffer(newText)`（选中并替换整个当前输入行，光标移至末尾）。
5. **方向键历史**：在 `richTextBoxConsole_KeyDown` 开头、原有“输入键回车到末尾”逻辑之前插入历史导航分支——仅当 `m_isInputEnabled AndAlso inputStart >= 0 AndAlso SelectionStart >= inputStart` 时生效，上键 `historyIndex` 前移并 `ReplaceInputBuffer`，下键后移（到末尾则清空），并 `e.SuppressKeyPress = True; e.Handled = True; Return`。光标在只读区时不拦截，沿用原有滚动导航。
6. **记录历史**：在现有 Enter 提交分支（读取 `input` 后）将非空 `input` 加入 `inputHistory`，并将 `historyIndex` 重置为 `inputHistory.Count`，使下一次上键从最近一条命令开始。

## 性能与可靠性

- 所有新增逻辑均为 O(1) 的 UI 事件处理，无循环/查询开销；`ReplaceInputBuffer` 通过一次性 `SelectedText` 赋值替换整行，避免逐字符操作与多余重绘。
- 历史列表为进程内内存列表，命令数量有限，无内存风险。
- 通过 `Clipboard.ContainsText()` 先判断再读取，避免空引用；插入/替换前均校验 `inputStart >= 0`，在只读态或进程未运行时安全跳过。

## 关键代码结构（VB.NET）

```
' 字段
Private inputHistory As New List(Of String)
Private historyIndex As Integer = 0

' 鼠标交互（Handles richTextBoxConsole.MouseUp）
Private Sub richTextBoxConsole_MouseUp(sender As Object, e As MouseEventArgs) Handles richTextBoxConsole.MouseUp

' 将文本插入当前输入缓冲（光标在输入区内则插到光标处，否则插到输入行末尾）
Private Sub InsertIntoInputBuffer(text As String)

' 用 newText 整体替换当前输入行，并将光标移到行尾
Private Sub ReplaceInputBuffer(newText As String)
```

## 架构与目录结构

本任务为单一控件的局部功能增强，不涉及架构调整。仅修改如下文件：

```
g:/mini-R/src/console/console/WinForm/ConsoleControl.vb   # [MODIFY] 新增两个字段、New() 中设置 ContextMenuStrip 抑制原生右键菜单、新增 MouseUp 处理与 InsertIntoInputBuffer/ReplaceInputBuffer 辅助方法，并在 KeyDown 中加入上下方向键历史导航、在 Enter 提交处记录输入历史。保持既有 Handles 事件绑定风格，不改动 designer.vb。
```