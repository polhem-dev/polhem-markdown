# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.Markdown 各套件值得注意的變更，每個版本只列幾項重點。自 0.2.0 起，每個版本都連到
[`docs/zh-TW/changelogs/`](docs/zh-TW/changelogs/) 下的發佈說明，那裡完整描述每一項變更，並說明每項行為變更影響誰、
該怎麼處理。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

- Avalonia 的 `MarkdownEditor` 可以讀取與設定選取範圍（`SelectionStart`、`SelectionLength`、`SelectedText`、
  `Select`），並在選取範圍或游標移動時通知（`SelectionChanged`）。`Replace` 取代一個範圍，或一次取代多個範圍
  （`MarkdownReplacement`，offset 以取代前的文字為準），都只算一個復原步驟，並保留游標、選取範圍與復原紀錄。
  Blazor 編輯器尚未提供這些成員。

## [0.4.1] - 2026-11-27

詳細內容：[docs/zh-TW/changelogs/0.4.1.md](docs/zh-TW/changelogs/0.4.1.md)

- `IsStreaming` 會補完寫在同一行的顯示數學（`$$x = 1$$`）之後的文字。0.4.0 把這一行當成一個沒有結束的數學區塊開頭，使得之後的
  連結、圖片、強調與表格標題列在回覆送達時露出原始 Markdown。

## [0.4.0] - 2026-11-26

詳細內容：[docs/zh-TW/changelogs/0.4.0.md](docs/zh-TW/changelogs/0.4.0.md)

- 兩個套件新增 `MarkdownReader`：把 viewer 排在置中的欄位中捲動，邊緣附目錄，可浮在文字上或釘選在旁邊。兩個檢視器都會列出
  文字中的標題（`Headings`），並能捲動到指定標題（`ScrollToHeading`、`ScrollToHeadingAsync`）。
- 文件內搜尋：Avalonia 有搜尋列（Ctrl+F、⌘F），兩個檢視器有相同的搜尋 API。
- 圖片可用原尺寸開啟、縮放與平移（`ZoomImages`），並有儲存原始檔的按鈕。
- 兩個檢視器會把仍在送達的回覆依完成後的樣子呈現（`IsStreaming`），Blazor 檢視器只重新渲染變更影響到的區塊。
- Avalonia 檢視器可跨區塊選取與複製文字，並告訴螢幕閱讀器文字各部分是什麼。沒有連結的段落不再是 Tab 停駐點。

## [0.3.1] - 2026-11-25

詳細內容：[docs/zh-TW/changelogs/0.3.1.md](docs/zh-TW/changelogs/0.3.1.md)

- `Polhem.Markdown.Blazor` 標記為支援裁剪與 AOT，並在 Blazor WebAssembly 應用程式中以裁剪方式驗證。只裁剪標記為可裁剪組件的
  應用程式（`TrimMode=partial`）現在也會裁剪這個組件。

## [0.3.0] - 2026-11-25

詳細內容：[docs/zh-TW/changelogs/0.3.0.md](docs/zh-TW/changelogs/0.3.0.md)

- `Polhem.Markdown.Blazor` 新增 `MarkdownEditor`，在原生 textarea 中編輯 Markdown 原始碼，依檢視器讀取文字的方式上色，
  有格式指令、清單按鍵與自己的復原紀錄，並可與檢視器同步。
- 兩個編輯器都顯示工具列（`ShowToolbar`，預設開啟；`ToolbarLabels` 替換英文標籤），Blazor 編輯器也有了與 Avalonia 版相同的
  行號（`ShowLineNumbers`）。
- `MarkdownFormat` 新增 `Image`、`Table` 與 `HorizontalRule`；編輯器新增標題循環（`CycleHeading`）。
- Blazor 的 `MarkdownViewer` 能在原始碼位置與捲動位置之間換算，並把按住命令鍵的點選回報為要求跳到原始碼。
- 升級的應用會在 Avalonia 編輯器中看到多出的工具列，Tab 也不再停在編輯器本身。

## [0.2.1] - 2026-11-24

詳細內容：[docs/zh-TW/changelogs/0.2.1.md](docs/zh-TW/changelogs/0.2.1.md)

- `Polhem.Markdown.Avalonia` 不再匯出 0.2.0 起多出的 `CompiledAvaloniaXaml.!XamlLoader` 與
  `CompiledAvaloniaXaml.!AvaloniaResources` 型別。

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

[Unreleased]: https://github.com/polhem-dev/polhem-markdown/compare/v0.4.1...HEAD
[0.4.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.3.1...v0.4.0
[0.3.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/polhem-dev/polhem-markdown/releases/tag/v0.1.0
