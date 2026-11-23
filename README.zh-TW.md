# Polhem.Markdown

[English](README.md) | **繁體中文**

[![Build CI](https://github.com/polhem-dev/polhem-markdown/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-markdown/actions/workflows/build-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=alert_status)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=bugs)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=vulnerabilities)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=code_smells)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=coverage)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)

.NET 的 Markdown 檢視器，讓同一段文字在不同前端呈現一致。一個套件負責解析並套用安全政策，各 UI 框架的檢視器套件負責呈現結果。

| 套件 | 用途 |
|------|------|
| `Polhem.Markdown` | 解析（使用 [Markdig](https://github.com/xoofx/markdig)），以及各檢視器共用的安全政策 |
| `Polhem.Markdown.Avalonia` | Avalonia 的檢視器控制項 |
| `Polhem.Markdown.Blazor` | Blazor 的檢視器元件，輸出以 CSS 變數設定樣式的語意化 HTML |

套件發佈在 [nuget.org](https://www.nuget.org/packages?q=Polhem.Markdown)，各版變更見 [CHANGELOG](CHANGELOG.zh-TW.md)。
目前是 0.x：minor 版仍可能變更 API，CHANGELOG 會寫明。

## 範例

`samples/MarkdownGallery` 以 Semi 主題（亮色或暗色）展示每一種節點，並可「重播」：把文字分段打出來，模擬串流回覆。

```bash
dotnet run --project samples/MarkdownGallery.Desktop
dotnet run --project samples/MarkdownGallery.Blazor
```

Blazor 版 Gallery 顯示同一份文字，方便兩個檢視器並排比對。

iOS head 需要 iOS workload，所以不在 solution 裡。在 Mac 上建置並於模擬器啟動：

```bash
dotnet build samples/MarkdownGallery.iOS -c Release -p:RuntimeIdentifier=iossimulator-arm64
dotnet build samples/MarkdownGallery.iOS -c Release -t:Run -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=":v2:udid=<模擬器 UDID>"
```

若建置訊息說 iOS workload 需要另一個 Xcode 版本，建置時以 `DEVELOPER_DIR` 指向該版本。

## 建置

```bash
dotnet build Polhem.Markdown.slnx --configuration Release
dotnet test Polhem.Markdown.slnx --configuration Release --no-build
```

## 授權

[MIT](LICENSE)
