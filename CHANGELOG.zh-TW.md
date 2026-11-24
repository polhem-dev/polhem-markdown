# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.Markdown 各套件值得注意的變更，每個版本只列幾項重點。自 0.2.0 起，每個版本都連到
[`docs/zh-TW/changelogs/`](docs/zh-TW/changelogs/) 下的發佈說明，那裡完整描述每一項變更，並說明每項行為變更影響誰、
該怎麼處理。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

- `Polhem.Markdown.Avalonia` 不再匯出 0.2.0 起多出的 `CompiledAvaloniaXaml.!XamlLoader` 與
  `CompiledAvaloniaXaml.!AvaloniaResources` 型別：編輯器的主題改由程式碼建立，不再使用 XAML（#48）。

## [0.2.0] - 2026-11-24

詳細內容：[docs/zh-TW/changelogs/0.2.0.md](docs/zh-TW/changelogs/0.2.0.md)

- 兩個檢視器支援 Mermaid 圖、程式碼上色、圖片、數學公式、註腳、GitHub 警示、刪除線、任務清單、emoji 短碼與標題錨點，
  README 風格的 raw HTML 經白名單轉成節點。
- 相對連結與圖片依 `MarkdownOptions.BaseUri` 解析。
- `Polhem.Markdown.Avalonia` 新增 Markdown 原始碼編輯器 `MarkdownEditor`，附格式指令（`Polhem.Markdown.MarkdownFormat`），
  檢視器可與它同步捲動。
- `MarkdownViewer.CjkFontFamily` 讓 Avalonia 檢視器的粗體中日韓文字維持粗體。
- `Polhem.Markdown.Avalonia` 新增相依 `Svg.Skia` 與 `Avalonia.AvaloniaEdit`。
- 圖片預設會載入，白名單內的 raw HTML 改為呈現，不再以文字顯示。
- 大量巢狀的行內語法不再造成堆疊溢位、使應用程式崩潰。

## [0.1.0] - 2026-11-21

### 新增

- `Polhem.Markdown` 以 Markdig 解析 Markdown，轉成檢視器呈現的模型：標題、段落、強調、行內程式碼、連結、清單、
  程式碼區塊、引用、表格與分隔線。raw HTML 以文字顯示，圖片顯示其替代文字，只有 scheme 在允許清單內的連結可以點選
  （`MarkdownOptions.AllowedLinkSchemes`，預設為 `http`、`https`、`mailto`）。
- `Polhem.Markdown.Avalonia` 新增 `MarkdownViewer` 控制項，顯示 Markdown 文字：文字可選取、連結可點選（會向上傳遞的
  `LinkActivated` 事件）、程式碼區塊附複製鈕，並支援表格。內容變更時只重建原文有變動的區塊。
- `Polhem.Markdown.Blazor` 新增 `MarkdownViewer` 元件，把 Markdown 文字呈現為語意化 HTML：連結以 `OnLinkActivated`
  交給宿主、程式碼區塊附複製鈕，樣式以 `--pmd-*` CSS 變數設定。

[Unreleased]: https://github.com/polhem-dev/polhem-markdown/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/polhem-dev/polhem-markdown/releases/tag/v0.1.0
