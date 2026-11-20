# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.Markdown 各套件值得注意的變更。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

### 新增

- `Polhem.Markdown` 以 Markdig 解析 Markdown，轉成檢視器呈現的模型：標題、段落、強調、行內程式碼、連結、清單、
  程式碼區塊、引用、表格與分隔線。raw HTML 以文字顯示，圖片顯示其替代文字，只有 scheme 在允許清單內的連結可以點選
  （`MarkdownOptions.AllowedLinkSchemes`，預設為 `http`、`https`、`mailto`）。
- `Polhem.Markdown.Avalonia` 新增 `MarkdownViewer` 控制項，顯示 Markdown 文字：文字可選取、連結可點選（會向上傳遞的
  `LinkActivated` 事件）、程式碼區塊附複製鈕，並支援表格。內容變更時只重建原文有變動的區塊。
- `Polhem.Markdown.Blazor` 新增 `MarkdownViewer` 元件，把 Markdown 文字呈現為語意化 HTML：連結以 `OnLinkActivated`
  交給宿主、程式碼區塊附複製鈕，樣式以 `--pmd-*` CSS 變數設定。
