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
  and `mailto`) become links. Relative links (`CHANGELOG.md`,
  `../README.md`) stay text unless `Options.BaseUri` is set, such as to the address of the document being shown; they
  then resolve against it by the standard URI rules, and the resolved link must still use an allowed scheme.
- **Links**: with `OnLinkActivated` set, a click does not navigate and the host decides what the link does; without it,
  the browser opens the link in a new tab. A link to a heading of the same text (`[setup](#setup)`, with the id GitHub
  gives the heading) is not reported: the click scrolls the heading to the top and moves focus to it. The id is not
  written as an HTML `id`, so it cannot clash with the page. Footnotes (`text[^note]` and `[^note]: ...`) are numbered
  by first reference and shown at the end, as on GitHub; a reference moves to its note and the note's `↩` back to the
  reference in the same way. The first click on any of these links loads the package's small
  `_content/Polhem.Markdown.Blazor/polhem-markdown.js` module; the page does not link it, but a Content Security Policy
  must allow scripts from the site itself.
- **Code blocks** show their language and a copy button; set `CopyCodeLabel` to translate its tooltip and accessible
  name. The copy uses the browser's clipboard, which needs a secure context (HTTPS or localhost). Code in C#, JSON, XML (with HTML, XAML and project files), SQL, shell and PowerShell, JavaScript and TypeScript, Python, YAML, CSS and Razor
  is highlighted by the component itself, with no script, the same way as in the Avalonia viewer; code in any other
  language, or none, stays plain text.
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
| `--pmd-code-keyword` | Keywords in highlighted code | `#8b5cf6` |
| `--pmd-code-string` | Strings in highlighted code | `#22a35a` |
| `--pmd-code-comment` | Comments in highlighted code | `#8a8f98` |
| `--pmd-code-constant` | Numbers and constants such as `true` and `null` | `#d97706` |
| `--pmd-code-tag` | Markup tag names | `#e5484d` |
| `--pmd-code-property` | Attribute names, JSON and YAML keys, CSS properties, shell variables | `#3b82f6` |

The defaults read on light and dark pages and match the Avalonia viewer.

The package is at 0.x: a minor version may still change the API. See the [repository](https://github.com/polhem-dev/polhem-markdown).
