# Polhem.Markdown

[English](README.md) | **繁體中文**

[![Build CI](https://github.com/polhem-dev/polhem-markdown/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-markdown/actions/workflows/build-ci.yml)

.NET 的 Markdown 檢視器，讓同一段文字在不同前端呈現一致。一個套件負責解析並套用安全政策，各 UI 框架的檢視器套件負責呈現結果。

| 套件 | 用途 |
|------|------|
| `Polhem.Markdown` | 解析（使用 [Markdig](https://github.com/xoofx/markdig)），以及各檢視器共用的安全政策 |
| `Polhem.Markdown.Avalonia` | Avalonia 的檢視器控制項 |
| `Polhem.Markdown.Blazor` | Blazor 的檢視器元件，輸出以 CSS 變數設定樣式的語意化 HTML |

這些套件仍在開發中，尚未發佈，API 也還沒定案。

## 建置

```bash
dotnet build Polhem.Markdown.slnx --configuration Release
dotnet test Polhem.Markdown.slnx --configuration Release --no-build
```

## 授權

[MIT](LICENSE)
