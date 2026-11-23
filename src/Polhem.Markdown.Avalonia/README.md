# Polhem.Markdown.Avalonia

A Markdown viewer control for Avalonia. It renders the same model as `Polhem.Markdown.Blazor`, so an Avalonia and a
Blazor front end show the same text the same way.

```xml
<Window xmlns:md="using:Polhem.Markdown.Avalonia">
  <md:MarkdownViewer Markdown="{Binding Reply}" LinkActivated="OnLinkActivated" />
</Window>
```

- **Safety**: raw HTML shows as text, images as their alternative text, and only links whose scheme is in
  `Options.AllowedLinkSchemes` (by default `http`, `https` and `mailto`) can be clicked.
- **Links**: a click raises the bubbling `LinkActivated` event. When no handler marks it handled, the viewer asks the
  operating system to open the link. A link to a heading of the same text (`[setup](#setup)`, with the id GitHub gives
  the heading) is not reported: the viewer scrolls the nearest scroll viewer so the heading is at the top.
- **Code blocks** show their language and a copy button; set `CopyCodeLabel` to translate its tooltip.
- **Mermaid diagrams**: a ` ```mermaid ` block with a flowchart or a sequence diagram is drawn by the viewer itself, with
  no script, centered, and scaled down when it is wider than the viewer. Boxes use `CodeBackground`, lifelines `RuleBrush`, and
  lines and text the foreground. A diagram of another type, or one using syntax the viewer does not support, shows as
  code; set `Options.RenderMermaidDiagrams` to `false` to show every diagram as code.
- **Colors** work on light and dark themes. Set `LinkForeground`, `CodeBackground`, `RuleBrush` and the alert brushes
  (`AlertNoteBrush`, `AlertTipBrush`, `AlertImportantBrush`, `AlertWarningBrush`, `AlertCautionBrush`) to match a theme;
  they apply without rebuilding the view.
- **Updates**: when `Markdown` changes, only the top-level blocks whose source changed are rebuilt, so a reply that
  grows as it arrives does not flicker.

The package is at 0.x: a minor version may still change the API. See the [repository](https://github.com/polhem-dev/polhem-markdown).
