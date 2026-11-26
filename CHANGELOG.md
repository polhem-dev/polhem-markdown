# Changelog

**English** | [繁體中文](CHANGELOG.zh-TW.md)

Notable changes to the Polhem.Markdown packages, a few lines per version. From 0.2.0, each version links to its release
notes under [`docs/en/changelogs/`](docs/en/changelogs/), which describe every change in full and, for each change in
behaviour, who it affects and what to do. The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and versions follow [Semantic Versioning](https://semver.org/).

## [Unreleased]

- Both viewers show a reply that is still arriving as it will look once complete (`IsStreaming`): the syntax its last
  block leaves open is closed or held back, so markers, partial addresses and lines of pipes no longer show and then
  jump (#69).
- `MarkdownReader`, in both packages, scrolls a viewer in a centred column and offers a table of contents: a rail along
  its edge with a line for each heading, and a list that slides out over the text when the rail is pointed at or
  clicked, or is pinned beside it (`OutlinePinned`, which binds both ways), on the left or the right (`OutlineSide`).
  The rail and the list mark the section being read. The Blazor list's background is `--pmd-outline-bg` (#79).
- The Avalonia viewer keeps a heading or footnote it moves the focus to at the top of the view. The scroll viewer used
  to move it out of place again as it brought the focused text into view.
- Both viewers can open an image at full size over the window, to zoom and pan (`ZoomImages`, off by default); a host
  shows images its own way with `ImageActivated` (Avalonia) or `OnImageActivated` (Blazor). The Blazor dialog's
  backdrop is `--pmd-zoom-backdrop` (#72).
- In the Avalonia viewer, a click on an image inside a link opens the link. It used to do nothing.
- The Avalonia viewer tells screen readers what the Blazor viewer's HTML does: headings with their level, links,
  lists and list items, tables, images by their alternative text, and alerts, quotes and collapsible sections as named
  groups. Tab moves through links and buttons in the order of the text, and Enter follows a link (#70).
- In the Avalonia viewer, a paragraph without links is no longer a Tab stop; a click still gives it the focus for
  selecting and copying. The Blazor viewer marks an alert with `role="note"`.
- Both viewers list the text's headings for a table of contents or an outline (`Headings`, with `HeadingsChanged` on
  Avalonia and `OnHeadingsChanged` on Blazor) and scroll one to the top of the view (`ScrollToHeading`,
  `ScrollToHeadingAsync`). `MarkdownHeading` gives each heading's level, text, id and source offset (#71).
- The Avalonia viewer scrolls a heading or a footnote it moves to back up to the top of the view. When the scroll
  viewer was shorter than the window, moving up left the target above the view by the difference.
- The Avalonia `MarkdownViewer` selects text across blocks, as a browser does: a drag runs from one paragraph, list
  item, table cell or code block into the next and scrolls at the edge of the view, Ctrl+A (Cmd+A) selects the whole
  text, and copying gives plain text in document order (#1).
- The Avalonia `MarkdownViewer` copies an image as its alternative text and a formula as its LaTeX (`$…$`). On
  Avalonia 12.0, copied text no longer loses or shifts characters after an image or a formula, and a triple click
  selects such a paragraph to its end.

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

[Unreleased]: https://github.com/polhem-dev/polhem-markdown/compare/v0.3.1...HEAD
[0.3.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.3.0...v0.3.1
[0.3.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.2.1...v0.3.0
[0.2.1]: https://github.com/polhem-dev/polhem-markdown/compare/v0.2.0...v0.2.1
[0.2.0]: https://github.com/polhem-dev/polhem-markdown/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/polhem-dev/polhem-markdown/releases/tag/v0.1.0
