# Polhem.Markdown

**English** | [繁體中文](README.zh-TW.md)

[![Build CI](https://github.com/polhem-dev/polhem-markdown/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-markdown/actions/workflows/build-ci.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=alert_status)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)
[![Bugs](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=bugs)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)
[![Vulnerabilities](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=vulnerabilities)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)
[![Code Smells](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=code_smells)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=polhem-dev_polhem-markdown&metric=coverage)](https://sonarcloud.io/project/overview?id=polhem-dev_polhem-markdown)

Markdown viewers for .NET that show the same text the same way on different front ends. One package parses the text
and applies the safety policy; a viewer package for each UI framework renders the result.

| Package | For |
|---------|-----|
| `Polhem.Markdown` | Parsing (with [Markdig](https://github.com/xoofx/markdig)) and the safety policy shared by the viewers |
| `Polhem.Markdown.Avalonia` | A viewer control for Avalonia |
| `Polhem.Markdown.Blazor` | A viewer component for Blazor, rendering semantic HTML styled with CSS variables |

The packages are published on [nuget.org](https://www.nuget.org/packages?q=Polhem.Markdown); the changes of each release
are in the [CHANGELOG](CHANGELOG.md). They are at 0.x: a minor version may still change the API, and the CHANGELOG says
when it does.

## Samples

`samples/MarkdownGallery` shows every kind of node on a Semi theme, light or dark, with a replay that types the text in
chunks as a streamed reply arrives.

```bash
dotnet run --project samples/MarkdownGallery.Desktop
dotnet run --project samples/MarkdownGallery.Blazor
```

The Blazor gallery shows the same text, so the two viewers can be compared side by side.

The iOS head is not in the solution, because it needs the iOS workload. On a Mac, build and start it on a simulator:

```bash
dotnet build samples/MarkdownGallery.iOS -c Release -p:RuntimeIdentifier=iossimulator-arm64
dotnet build samples/MarkdownGallery.iOS -c Release -t:Run -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=":v2:udid=<simulator UDID>"
```

If the build says the iOS workload needs another Xcode version, point `DEVELOPER_DIR` at that version for the build.

## Building

```bash
dotnet build Polhem.Markdown.slnx --configuration Release
dotnet test Polhem.Markdown.slnx --configuration Release --no-build
```

## License

[MIT](LICENSE)
