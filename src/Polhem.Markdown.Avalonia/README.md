# Polhem.Markdown.Avalonia

A Markdown viewer control for Avalonia. It renders the same model as `Polhem.Markdown.Blazor`, so an Avalonia and a
Blazor front end show the same text the same way.

```xml
<Window xmlns:md="using:Polhem.Markdown.Avalonia">
  <md:MarkdownViewer Markdown="{Binding Reply}" LinkActivated="OnLinkActivated" />
</Window>
```

- **Safety**: raw HTML shows as text, and only links whose scheme is in
  `Options.AllowedLinkSchemes` (by default `http`, `https` and `mailto`) can be clicked. Relative links (`CHANGELOG.md`,
  `../README.md`) stay text unless `Options.BaseUri` is set, such as to the address of the document being shown; they
  then resolve against it by the standard URI rules, and the resolved link must still use an allowed scheme.
- **Images** whose source uses a scheme in `Options.AllowedImageSchemes` (by default `https`; clear it to show none, or
  add `file`) load and show at their own size, scaled down to fit; relative sources resolve against `Options.BaseUri`.
  Any other image shows its alternative text, as one does while it loads and when it cannot be loaded. The viewer
  fetches `http` and `https` images and reads `file` ones itself, at most 10 MB each, and remembers them, so a reply that
  is still arriving does not fetch them again; set `ImageLoader` to load them another way, for example with
  authentication or through a proxy. SVG images are drawn with Svg.Skia, which this package depends on, only from the
  badge hosts in `TrustedSvgHosts` (by default the ones VS Code accepts SVG badges from, such as `img.shields.io`) and
  GitHub's workflow badges, without fetching anything they refer to; any other SVG shows its alternative text, because
  a hostile SVG can exhaust a renderer's processor time and memory. The Blazor viewer shows every allowed SVG, since
  the browser draws it in its own sandbox.
- **Links**: a click raises the bubbling `LinkActivated` event. When no handler marks it handled, the viewer asks the
  operating system to open the link. A link to a heading of the same text (`[setup](#setup)`, with the id GitHub gives
  the heading) is not reported: the viewer scrolls the nearest scroll viewer so the heading is at the top.
- **Footnotes** (`text[^note]` and `[^note]: ...`) are numbered by first reference and shown at the end in a slightly
  smaller font, as on GitHub. A reference moves to its note and the note's `↩` back to the reference, in the same way.
- **Code blocks** show their language and a copy button; set `CopyCodeLabel` to translate its tooltip. Code in
  C#, JSON, XML (with HTML, XAML and project files), SQL, shell and PowerShell, JavaScript and TypeScript, Python, YAML, CSS and Razor is highlighted, the same way as in the Blazor viewer; code in any other language, or none, stays plain text.
- **Mermaid diagrams**: a ` ```mermaid ` block with a flowchart or a sequence diagram is drawn by the viewer itself, with
  no script, centered, and scaled down when it is wider than the viewer. Boxes use `CodeBackground`, lifelines `RuleBrush`, and
  lines and text the foreground. A diagram of another type, or one using syntax the viewer does not support, shows as
  code; set `Options.RenderMermaidDiagrams` to `false` to show every diagram as code.
- **Colors** work on light and dark themes. Set `LinkForeground`, `CodeBackground`, `RuleBrush`, the alert brushes
  (`AlertNoteBrush`, `AlertTipBrush`, `AlertImportantBrush`, `AlertWarningBrush`, `AlertCautionBrush`) and the code
  brushes (`CodeKeywordBrush`, `CodeStringBrush`, `CodeCommentBrush`, `CodeConstantBrush`, `CodeTagBrush`,
  `CodePropertyBrush`) to match a theme; they apply without rebuilding the view.
- **Updates**: when `Markdown` changes, only the top-level blocks whose source changed are rebuilt, so a reply that
  grows as it arrives does not flicker.

The package is at 0.x: a minor version may still change the API. See the [repository](https://github.com/polhem-dev/polhem-markdown).
