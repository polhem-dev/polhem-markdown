# Polhem.Markdown

**English** | [繁體中文](README.zh-TW.md)

[![Build CI](https://github.com/polhem-dev/polhem-markdown/actions/workflows/build-ci.yml/badge.svg)](https://github.com/polhem-dev/polhem-markdown/actions/workflows/build-ci.yml)

Markdown viewers for .NET that show the same text the same way on different front ends. One package parses the text
and applies the safety policy; a viewer package for each UI framework renders the result.

| Package | For |
|---------|-----|
| `Polhem.Markdown` | Parsing (with [Markdig](https://github.com/xoofx/markdig)) and the safety policy shared by the viewers |
| `Polhem.Markdown.Avalonia` | A viewer control for Avalonia |
| `Polhem.Markdown.Blazor` | A viewer component for Blazor, rendering semantic HTML styled with CSS variables |

The packages are in development and not yet published; their API is not settled.

## Building

```bash
dotnet build Polhem.Markdown.slnx --configuration Release
dotnet test Polhem.Markdown.slnx --configuration Release --no-build
```

## License

[MIT](LICENSE)
