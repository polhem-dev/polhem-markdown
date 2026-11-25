# Polhem.Markdown.Blazor

A Markdown viewer component for Blazor that renders semantic HTML styled with CSS variables, and a Markdown source
editor component. The viewer renders the same model as `Polhem.Markdown.Avalonia`, so a Blazor and an Avalonia front
end show the same text the same way, and the editor colors and edits the source the way the Avalonia editor does.

Link the stylesheet in the host page, then use the components:

```html
<link rel="stylesheet" href="_content/Polhem.Markdown.Blazor/polhem-markdown.css" />
```

```razor
@using Polhem.Markdown.Blazor

<MarkdownViewer Markdown="@reply" OnLinkActivated="OpenLink" />
```

## MarkdownViewer

- **Raw HTML** is never added as markup. The elements README files use become part of the text: `<img>` (with `width`
  and `height`), `<a href>`, `<br>`, `<b>`, `<strong>`, `<i>`, `<em>`, `<code>`, `<sup>`, `<sub>`, `<kbd>`, `<p>` and
  `<div>` with `align="center"` or `"right"` (classes `pmd-align-center` and `pmd-align-right`), `<details>` with
  `<summary>`, and `<picture>` with sources for `(prefers-color-scheme: dark)` and `light`, which the browser picks
  from. Any other HTML element's tag is dropped and its content kept, attributes other than those are dropped, and
  scripts, styles and comments are dropped with their content. Text that only looks like a tag (`List<string>`), and a
  tag that does not pair up within its paragraph, show as written.
- **Safety**: the markup is built with a render tree, never from an HTML string except a formula's MathML, which is
  written from the parsed formula with every text encoded. Only links whose scheme is in `Options.AllowedLinkSchemes` (by default `http`, `https`
  and `mailto`) become links. Relative links (`CHANGELOG.md`,
  `../README.md`) stay text unless `Options.BaseUri` is set, such as to the address of the document being shown; they
  then resolve against it by the standard URI rules, and the resolved link must still use an allowed scheme.
- **Images** whose source uses a scheme in `Options.AllowedImageSchemes` (by default `https`; clear it to show none)
  are `<img>` elements that load lazily and send no referrer, scaled down to fit; relative sources resolve against
  `Options.BaseUri`. Any other image shows its alternative text. SVG images are `<img>` too, where their scripts do not
  run.
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
- **Math** written as on GitHub, `$…$` in a line and `$$…$$` or a ` ```math ` block on its own, renders as MathML,
  which the browser lays out with no script: fractions, scripts, roots, Greek letters, operators, large operators with
  limits, `\left … \right` delimiters, matrices and `cases`, `\text` and accents. A formula using anything else shows
  its LaTeX as code. Dollar signs in prose, such as prices, stay text. Delimiters and large operators grow only with a
  math font, set by `--pmd-math-font`.
- **Mermaid diagrams**: a ` ```mermaid ` block with a flowchart or a sequence diagram renders as inline SVG laid out by
  the component, with no script, centered, and scales down when it is wider than the page. Boxes use `--pmd-code-bg`, lifelines
  `--pmd-rule`, and lines and text the text color. A diagram of another type, or one using syntax the viewer does not
  support, shows as code; set `Options.RenderMermaidDiagrams` to `false` to show every diagram as code.
- **Keeping in step with an editor**: `ScrollToSourceOffsetAsync` scrolls the view around the viewer (the nearest
  element that scrolls, or the page) so the text at a source offset is at its top; `FirstVisibleSourceOffsetChanged`
  reports the offset at the top as the view scrolls; and with `OnSourceRequested` set, a click with Ctrl (Command on a
  Mac) reports the source offset of what was clicked instead of doing anything else, such as opening a link. An offset
  is placed in the top-level block it falls in, in proportion to how far into the block's source it is. The viewer
  loads its script for these only when one of them is used.
- **Styles** inherit the font and the text color of the surrounding page. Everything is scoped under `.pmd`; `Class`
  adds classes to that element.

| Variable | Sets | Default |
|----------|------|---------|
| `--pmd-link` | Link color, and the fill of a checked task box | `#3b82f6` |
| `--pmd-code-bg` | Background of inline code, keys (`<kbd>`), code blocks and table headers | `rgba(128, 128, 128, 0.12)` |
| `--pmd-rule` | Rules, table borders, the bar beside a quote, and the border of a key and of an unchecked task box | `rgba(128, 128, 128, 0.35)` |
| `--pmd-code-font` | Font of inline code, keys and code blocks | `"Cascadia Mono", Consolas, Menlo, ui-monospace, monospace` |
| `--pmd-math-font` | Font of math; delimiters and large operators grow only in a font with math tables | `"STIX Two Math", "Cambria Math", "Latin Modern Math", math` |
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

## MarkdownEditor

`MarkdownEditor` edits Markdown as source, colored the way the viewer reads it. Put a `MarkdownViewer` beside it for a
live preview.

```razor
<MarkdownEditor @bind-Markdown="text" aria-label="Markdown source" />
```

- **Input** is a native `<textarea>`, so input methods, on-screen keyboards and screen readers work as anywhere on the
  page; attributes the component does not know, such as `aria-label`, go to the textarea. The colors are drawn behind
  it by the package's `polhem-markdown-editor.js` module, which the component loads itself. The editor fills the height
  its host gives it.
- **Text**: `Markdown` follows every edit through `MarkdownChanged`, so `@bind-Markdown` keeps a field up to date;
  setting another text replaces it and clears the undo history. A text with `\r\n` line breaks gets them back.
  `IsReadOnly` and `WordWrap` (on by default) do what they say. The editor keeps its own undo history, which Ctrl+Z
  (Command+Z), Ctrl+Shift+Z, Ctrl+Y, the browser's Edit menu, `UndoAsync` and `RedoAsync` use.
- **Coloring**: markers, headings (bold), emphasis (bold, italic, struck through), inline code, links and their
  addresses, raw HTML and formulas, and the code of fenced blocks with the viewer's highlighting. The text is scanned a
  moment after typing stops; until then the colors move with the edits. A long text waits a little longer, which
  matters under WebAssembly, where the scan runs on the page's only thread.
- **Formats**: `ApplyFormatAsync(MarkdownFormat)` applies a format to the selection, or removes it when the selection
  already has it, as one step to undo. Inline formats wrap the selection, or insert a pair at the caret; line formats
  apply to every line the selection touches. Ctrl, or Command on a Mac, with B, I, K and E applies bold, italic, a link
  and inline code.
- **Lists**: Enter on a list item starts the next one, and on an empty item ends the list. Tab and Shift+Tab move a list
  item in and out; elsewhere Tab moves the focus on, so a keyboard user can leave the editor.
- **Keeping in step with a viewer**: `ScrollToSourceOffsetAsync`, `FirstVisibleSourceOffsetChanged` and
  `FirstVisibleSourceOffset` work as the viewer's do, with the same offsets, so a host passes them from one to the
  other; `SetCaretOffsetAsync` and `FocusAsync` move the user there after `OnSourceRequested`. The repository's Blazor
  gallery does it in its Edit mode.
- **Rendering**: it works under interactive server, WebAssembly and auto rendering. Rendered statically, it shows the
  text read-only.
- **Links** in the source cannot be opened from the editor; the viewer opens them after checking their scheme.
- **Unlike the Avalonia editor**, it has no line numbers and no search panel; the browser's own find searches the page.

Besides `--pmd-code-font`, `--pmd-link` and the `--pmd-code-*` token colors, the editor uses:

| Variable | Sets | Default |
|----------|------|---------|
| `--pmd-heading` | Headings | `#8b5cf6` |
| `--pmd-marker` | Syntax such as heading and emphasis markers, list bullets and fences, and raw HTML | `#8a8f98` |
| `--pmd-inline-code` | Inline code | `#e5484d` |
| `--pmd-url` | Link destinations and bare URLs | `#22a35a` |
| `--pmd-math` | Formulas | `#d97706` |
| `--pmd-selection` | The background of selected text, which must let the text show through | `rgba(59, 130, 246, 0.3)` |
| `--pmd-caret` | The caret | the text color |

Keep `--pmd-code-font` monospaced: the colored text is drawn behind the textarea and must line up with it, which needs
bold and italic letters as wide as regular ones.

The package is at 0.x: a minor version may still change the API. See the [repository](https://github.com/polhem-dev/polhem-markdown).
