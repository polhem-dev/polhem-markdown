# Changelog

**English** | [繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem.Markdown packages, a few lines per version. From 0.2.0, each version links to its release
notes under [`docs/en/changelogs/`](docs/en/changelogs/), which describe every change in full and, for each change in
behaviour, who it affects and what to do. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

- The Avalonia `MarkdownEditor` reads and sets the selection (`SelectionStart`, `SelectionLength`, `SelectedText`,
  `Select`) and reports when it or the caret moves (`SelectionChanged`). `Replace` changes one range, or several at once
  (`MarkdownReplacement`, offsets in the text before any of them), as one step to undo, keeping the caret, the
  selection and the undo history. The Blazor editor does not have these yet.
- The Avalonia `MarkdownEditor` draws marks a host puts over ranges of the source (`AddMark`, with
  `MarkdownRangeMarkStyle` `Background`, `Underline` or `Strikethrough` and a brush), under the selection and apart
  from the syntax colors. Marks move with the edits, go away with their text, and are cleared together by an owner key
  (`ClearMarks`). `GetTextBounds` tells where an offset or a range is in the editor, and `ViewChanged` when that may
  have moved, for a popup placed next to the text. The gallery previews a change as a diff. The Blazor editor does not
  have these yet.

## [0.4.1] - 2026-11-27

Details: [docs/en/changelogs/0.4.1.md](docs/en/changelogs/0.4.1.md)

- `IsStreaming` completes the text after display math written on one line (`$$x = 1$$`). 0.4.0 took that line for the
  start of a math block that never closed, so a link, image, emphasis or table header row after it showed its Markdown
  while the reply arrived.

## [0.4.0] - 2026-11-26

Details: [docs/en/changelogs/0.4.0.md](docs/en/changelogs/0.4.0.md)

- `MarkdownReader`, in both packages, scrolls a viewer in a centred column with a table of contents along its edge,
  which slides out over the text or is pinned beside it. Both viewers list the text's headings (`Headings`) and scroll
  to one (`ScrollToHeading`, `ScrollToHeadingAsync`).
- Finding text: a find bar on Avalonia (Ctrl+F, ⌘F), and the same find API on both viewers.
- Images open at full size to zoom and pan (`ZoomImages`), with a button that saves the original file.
- Both viewers show a reply that is still arriving as it will look once complete (`IsStreaming`), and the Blazor
  viewer renders only the blocks a change affects.
- The Avalonia viewer selects and copies text across blocks, and tells screen readers what each part of the text is.
  A paragraph without links is no longer a Tab stop.

## [0.3.1] - 2026-11-25

Details: [docs/en/changelogs/0.3.1.md](docs/en/changelogs/0.3.1.md)

- `Polhem.Markdown.Blazor` is marked trim and AOT compatible, and is checked under trimming in a Blazor WebAssembly
  app. An app that trims only the assemblies marked trimmable (`TrimMode=partial`) now trims this one too.

## [0.3.0] - 2026-11-25

Details: [docs/en/changelogs/0.3.0.md](docs/en/changelogs/0.3.0.md)

- `Polhem.Markdown.Blazor` adds `MarkdownEditor`, a Markdown source editor in a native textarea, colored as the viewers
  read the text, with formats, list keys and its own undo, and kept in step with the viewer.
- Both editors show a toolbar (`ShowToolbar`, on by default; `ToolbarLabels` replaces its English labels), and the
  Blazor editor has line numbers (`ShowLineNumbers`) as the Avalonia one does.
- `MarkdownFormat` adds `Image`, `Table` and `HorizontalRule`; the editors add a heading cycle (`CycleHeading`).
- The Blazor `MarkdownViewer` maps between source offsets and its scroll position, and reports a command-click as a
  request for the source.
- The Avalonia editor's toolbar is a new row for applications that upgrade, and Tab passes the editor itself by.

## [0.2.1] - 2026-11-24

Details: [docs/en/changelogs/0.2.1.md](docs/en/changelogs/0.2.1.md)

- `Polhem.Markdown.Avalonia` no longer exports the `CompiledAvaloniaXaml.!XamlLoader` and
  `CompiledAvaloniaXaml.!AvaloniaResources` types that 0.2.0 exported.

## [0.2.0] - 2026-11-24

Details: [docs/en/changelogs/0.2.0.md](docs/en/changelogs/0.2.0.md)

- Both viewers render Mermaid diagrams, highlighted code, images, math, footnotes, GitHub alerts, strikethrough, task
  lists, emoji shortcodes and links to headings, and turn README-style raw HTML into nodes through an allowlist.
- Relative links and images resolve against `MarkdownOptions.BaseUri`.
- `Polhem.Markdown.Avalonia` adds `MarkdownEditor`, a Markdown source editor with formatting commands
  (`Polhem.Markdown.MarkdownFormat`), and the viewer can be kept in step with it.
- `MarkdownViewer.CjkFontFamily` keeps bold CJK text bold in the Avalonia viewer.
- `Polhem.Markdown.Avalonia` depends on `Svg.Skia` and `Avalonia.AvaloniaEdit`.
- Images load by default, and allowed raw HTML renders instead of showing as text.
- Deeply nested inline syntax no longer overflows the stack and ends the application.

## [0.1.0] - 2026-11-21

### Added

- `Polhem.Markdown` parses Markdown with Markdig into the model the viewers render: headings, paragraphs, emphasis,
  inline code, links, lists, code blocks, quotes, tables and rules. Raw HTML shows as text, images as their alternative
  text, and only links with an allowed scheme stay clickable (`MarkdownOptions.AllowedLinkSchemes`, by default `http`,
  `https` and `mailto`).
- `Polhem.Markdown.Avalonia` adds `MarkdownViewer`, a control that shows a Markdown text with selectable text,
  clickable links (the bubbling `LinkActivated` event), code blocks with a copy button, and tables. It rebuilds only the
  blocks whose source changed.
- `Polhem.Markdown.Blazor` adds `MarkdownViewer`, a component that renders a Markdown text as semantic HTML, with
  `OnLinkActivated` for links, code blocks with a copy button, and a stylesheet set through `--pmd-*` CSS variables.

[Unreleased]: https://github.com/polhem-dev/polhem-markdown/compare/v0.4.1...HEAD
[0.4.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.4.0...v0.4.1
[0.4.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.3.1...v0.4.0
[0.3.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/polhem-dev/polhem-markdown/releases/tag/v0.1.0
