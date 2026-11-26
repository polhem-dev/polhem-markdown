# 變更紀錄

[English](CHANGELOG.md) | **繁體中文**

Polhem.Markdown 各套件值得注意的變更，每個版本只列幾項重點。自 0.2.0 起，每個版本都連到
[`docs/zh-TW/changelogs/`](docs/zh-TW/changelogs/) 下的發佈說明，那裡完整描述每一項變更，並說明每項行為變更影響誰、
該怎麼處理。格式依循 [Keep a Changelog](https://keepachangelog.com/zh-TW/1.1.0/)，版號依循
[語意化版本](https://semver.org/lang/zh-TW/)。

## [Unreleased]

- 文件內搜尋：在 Avalonia 檢視器按 Ctrl+F（⌘F）會叫出搜尋列並反白所有符合處，`MarkdownReader` 的細欄上有放大鏡按鈕。
  兩個檢視器有相同的 API（Avalonia 的 `Find`、`FindNext`、`FindPrevious`；Blazor 的 `FindAsync` 等），Blazor 上瀏覽器原生的
  搜尋也能用（#85）。
- 兩個檢視器都能把仍在送達的回覆依完成後的樣子呈現（`IsStreaming`）：最後一個區塊尚未結束的語法會先補齊或暫不顯示，
  不再先露出記號、半截網址或一整行管線再跳動（#69）。
- Blazor 檢視器只重新渲染文字變更影響到的頂層區塊，與 Avalonia 檢視器只重建這些區塊的做法相同：逐步增長的回覆只渲染最後幾個
  區塊，編輯只渲染改到的區塊。過去每次變更都會重新渲染整份文字。
- 兩個套件新增 `MarkdownReader`：捲動 viewer，把內文排在置中的欄位，並提供目錄。邊緣的細欄為每個標題畫一條短線，
  指向或點擊細欄時，標題清單會浮在文字上；釘選後成為文字旁的側欄（`OutlinePinned`，可雙向繫結），可放在左側或右側
  （`OutlineSide`）。清單上的圖釘在浮動時是斜放的外框，釘選後直立並填滿。
  細欄與清單都會標示正在閱讀的章節。Blazor 清單的背景色為 `--pmd-outline-bg`（#79）。
- Avalonia 檢視器把焦點移到標題或註腳時，目標會留在畫面頂端。過去 ScrollViewer 為了讓取得焦點的文字可見，會再捲動一次，使目標偏離頂端。
- 兩個檢視器都能把圖片以原尺寸蓋在視窗上顯示，可縮放與平移（`ZoomImages`，預設關閉）；宿主可透過 `ImageActivated`
  （Avalonia）或 `OnImageActivated`（Blazor）改用自己的方式顯示圖片。Blazor 對話框的背景色為 `--pmd-zoom-backdrop`（#72）。
- Avalonia 檢視器中，點選連結裡的圖片會開啟該連結。過去點了沒有反應。
- Avalonia 檢視器提供給螢幕閱讀器的資訊與 Blazor 檢視器的 HTML 相同：標題與其層級、連結、清單與清單項目、表格、以替代文字
  命名的圖片，以及具名群組形式的警示、引言與可收合區段。Tab 依文字順序在連結與按鈕之間移動，Enter 開啟連結（#70）。
- Avalonia 檢視器中沒有連結的段落不再是 Tab 停駐點；點選後仍會取得焦點，以便選取與複製。Blazor 檢視器的警示加上
  `role="note"`。
- 兩個檢視器都會列出文字中的標題，供目錄或大綱使用（`Headings`；Avalonia 以 `HeadingsChanged`、Blazor 以 `OnHeadingsChanged`
  通知變更），並可把某個標題捲到畫面頂端（`ScrollToHeading`、`ScrollToHeadingAsync`）。`MarkdownHeading` 提供每個標題的層級、
  文字、id 與原文位置（#71）。
- Avalonia 檢視器往上捲到標題或腳註時，會正確停在畫面頂端。過去捲動區域比視窗矮時，往上捲會讓目標超出畫面上緣，超出的距離就是兩者的高度差。
- Avalonia 的 `MarkdownViewer` 可以像瀏覽器一樣跨區塊選取文字：拖曳可以從一個段落、清單項目、表格儲存格或程式碼區塊延伸到下一個，
  拖到視窗邊緣時會自動捲動；Ctrl+A（Cmd+A）全選整份文字，複製時依文件順序產生純文字（#1）。
- Avalonia 的 `MarkdownViewer` 複製時，圖片會換成替代文字、公式會換成 LaTeX（`$…$`）。在 Avalonia 12.0 上，含圖片或公式的段落
  複製時不再遺漏或錯位字元，點三下也能選到段落結尾。

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

[Unreleased]: https://github.com/polhem-dev/polhem-markdown/compare/v0.3.1...HEAD
[0.3.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/polhem-dev/polhem-markdown/releases/tag/v0.1.0
