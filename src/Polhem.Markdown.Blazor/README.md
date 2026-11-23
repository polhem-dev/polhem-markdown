# Polhem.Markdown.Blazor

A Markdown viewer component for Blazor that renders semantic HTML styled with CSS variables. It renders the same model
as `Polhem.Markdown.Avalonia`, so a Blazor and an Avalonia front end show the same text the same way.

Link the stylesheet in the host page, then use the component:

```html
<link rel="stylesheet" href="_content/Polhem.Markdown.Blazor/polhem-markdown.css" />
```

```razor
@using Polhem.Markdown.Blazor

<MarkdownViewer Markdown="@reply" OnLinkActivated="OpenLink" />
```

- **Safety**: the markup is built with a render tree, never from an HTML string. Raw HTML shows as text, images as
  their alternative text, and only links whose scheme is in `Options.AllowedLinkSchemes` (by default `http`, `https`
  and `mailto`) become links.
- **Links**: with `OnLinkActivated` set, a click does not navigate and the host decides what the link does; without it,
  the browser opens the link in a new tab.
- **Code blocks** show their language and a copy button; set `CopyCodeLabel` to translate its tooltip and accessible
  name. The copy uses the browser's clipboard, which needs a secure context (HTTPS or localhost).
- **Mermaid diagrams**: a ` ```mermaid ` block with a flowchart or a sequence diagram renders as inline SVG laid out by
  the component, with no script, centered, and scales down when it is wider than the page. Boxes use `--pmd-code-bg`, lifelines
  `--pmd-rule`, and lines and text the text color. A diagram of another type, or one using syntax the viewer does not
  support, shows as code; set `Options.RenderMermaidDiagrams` to `false` to show every diagram as code.
- **Styles** inherit the font and the text color of the surrounding page. Everything is scoped under `.pmd`; `Class`
  adds classes to that element.

| Variable | Sets | Default |
|----------|------|---------|
| `--pmd-link` | Link color | `#3b82f6` |
| `--pmd-code-bg` | Background of inline code, code blocks and table headers | `rgba(128, 128, 128, 0.12)` |
| `--pmd-rule` | Rules, table borders and the bar beside a quote | `rgba(128, 128, 128, 0.35)` |
| `--pmd-code-font` | Font of inline code and code blocks | `"Cascadia Mono", Consolas, Menlo, ui-monospace, monospace` |
| `--pmd-alert-note` | Bar, icon and title of a `> [!NOTE]` alert | `#3b82f6` |
| `--pmd-alert-tip` | The same for `> [!TIP]` | `#22a35a` |
| `--pmd-alert-important` | The same for `> [!IMPORTANT]` | `#8b5cf6` |
| `--pmd-alert-warning` | The same for `> [!WARNING]` | `#d97706` |
| `--pmd-alert-caution` | The same for `> [!CAUTION]` | `#e5484d` |

The defaults read on light and dark pages and match the Avalonia viewer.

The package is at 0.x: a minor version may still change the API. See the [repository](https://github.com/polhem-dev/polhem-markdown).
