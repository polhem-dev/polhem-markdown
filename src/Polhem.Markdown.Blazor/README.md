# Polhem.Markdown.Blazor

A Markdown viewer component that renders semantic HTML styled with CSS variables, a reader component that adds a
table of contents around it, and a Markdown source editor component, for Blazor. The viewer renders the same model as `Polhem.Markdown.Avalonia`, so a Blazor and an Avalonia
front end show the same text the same way.

![The same reply in the Blazor viewer and the Avalonia viewer](https://raw.githubusercontent.com/polhem-dev/polhem-markdown/main/docs/images/viewer-hero.png)

Link the stylesheet in the host page:

```html
<link rel="stylesheet" href="_content/Polhem.Markdown.Blazor/polhem-markdown.css" />
```

## MarkdownViewer

Shows Markdown: GitHub Flavored Markdown, highlighted code, math as MathML, Mermaid diagrams as SVG and the HTML that
README files use, all drawn without a script. Raw HTML is never added as markup, and only links and images with an
allowed scheme become links and images.

```razor
@using Polhem.Markdown
@using Polhem.Markdown.Blazor
@inject NavigationManager Navigation

<MarkdownViewer Markdown="@reply" Options="options" OnLinkActivated="OpenLink" />

@code {
    private string reply = "# Hello";

    // Resolve relative links and images against the document's address.
    private readonly MarkdownOptions options = new() { BaseUri = new Uri("https://example.com/docs/") };

    // With OnLinkActivated set, a click does not navigate; without it, the link opens in a new tab.
    private void OpenLink(MarkdownLinkActivatedEventArgs e) => Navigation.NavigateTo(e.Uri.ToString());
}
```

The viewer inherits the font and the text color of the page. The other colors and fonts are CSS variables; their list
and defaults are at the top of `polhem-markdown.css`:

```css
.pmd {
  --pmd-link: #0969da;
  --pmd-code-font: "JetBrains Mono", monospace;
}
```

Or put the viewer in a `MarkdownReader`, below, which shows the table of contents itself.

To show a reply while it arrives, set the text again as it grows. Only the blocks that changed are rebuilt. Set
`IsStreaming` while it arrives, and the syntax its last block leaves open shows as it will once the reply is complete:
emphasis and code spans are closed, a link shows as its text until its address ends, an image or a tag still being
typed waits, and a table's header row shows as a table. Clear it when the reply ends, and the text shows exactly as
written.

```razor
<MarkdownViewer Markdown="@reply" IsStreaming="@arriving" />
```

The browser's own find (Ctrl+F, or Find in Page on a phone) works on the viewer's text. To search from a host's own
controls, `FindAsync` highlights every match and goes to the first, opening a closed section around it, and
`FindNextAsync`, `FindPreviousAsync` and `ClearFindAsync` go on from there; `FindMatchCount` and `CurrentFindMatch`
tell where. A match runs across emphasis and links, and finds an image by its alternative text and a formula by its
LaTeX, as on Avalonia. The highlights are `--pmd-find` and `--pmd-find-current`, drawn with the browser's CSS Custom
Highlight API, so a page shows the matches of one viewer at a time. Some browsers' own find does not look inside a
closed `<details>`; `FindAsync` does.

Set `ZoomImages` to open an image at full size over the page with a click or Enter, in a dialog where the wheel, a
pinch or + and - zoom it, a drag pans it, a double click switches between fitting the window and the image's own
size, and Escape or a click beside it closes it. An image inside a link opens the link instead. With
`OnImageActivated` set, the host shows the image its own way and the viewer opens nothing. The dialog's backdrop is
`--pmd-zoom-backdrop`.

The dialog's toolbar, at its top right, has a close button and a save button, which downloads the image's original
file. When the image's server does not let the page read it, the image opens in a new tab instead, where the browser
saves it. With `OnImageSaveRequested` set, the host saves the image its own way.

```razor
<MarkdownViewer Markdown="@reply" ZoomImages="true" />
```

`CodeBlockActions` adds a host's own buttons to code blocks, before the copy button, such as "Insert at cursor" or
"Run". Each receives the block's code and its language, and runs on the renderer's dispatcher in WebAssembly and on the
server alike. A button shows its label, or with `IconPath` an icon (SVG path data in a 24 by 24 box) with the label as
its tooltip and accessible name; the buttons are styled as the copy button, from the same variables. `IsVisible`
chooses the blocks that show it; when its answer depends on something else, call `RefreshCodeBlockActionsAsync` when
that changes. Keep the list in a field: a new list renders every code block again. The buttons are always shown, as the
copy button is, so they work on a touch screen.

```razor
<MarkdownViewer @ref="viewer" Markdown="@reply" CodeBlockActions="actions" />

@code {
    private MarkdownViewer? viewer;
    private IReadOnlyList<MarkdownCodeBlockAction>? actions;
    private string draft = "";

    protected override void OnInitialized() => actions =
    [
        new MarkdownCodeBlockAction("Insert into the draft", block =>
        {
            draft += block.Code;
            StateHasChanged();   // the action is a plain delegate, so the page renders itself again
        })
        {
            IsVisible = block => block.Language == "markdown",
        },
    ];
}
```

`Headings` lists the text's headings for a table of contents, `OnHeadingsChanged` reports a change in them, and
`ScrollToHeadingAsync` scrolls one to the top of the view and focuses it:

```razor
<MarkdownViewer @ref="viewer" Markdown="@reply" OnHeadingsChanged="h => headings = h"
                FirstVisibleSourceOffsetChanged="Follow" />

<nav>
    @foreach (var heading in headings)
    {
        <button class="@(heading == current ? "current" : "")"
                @onclick="() => viewer!.ScrollToHeadingAsync(heading.Id)">@heading.Text</button>
    }
</nav>

@code {
    private MarkdownViewer? viewer;
    private IReadOnlyList<MarkdownHeading> headings = [];
    private MarkdownHeading? current;

    // The section being read: the last heading that starts at or before the text at the top of the view.
    private void Follow(int offset) => current = headings.LastOrDefault(heading => heading.SourceOffset <= offset);
}
```

## MarkdownReader

A reading area for a viewer: it scrolls the viewer, keeps the text in a centred column (`TextMaxWidth`, 800 pixels by
default), and offers a table of contents. A narrow rail along its edge draws a short line for each heading and marks
the section being read. Pointing at the rail, or clicking it, slides the list of headings out over the text; the pin in
the list keeps it beside the text as a column, the text centring in the room that is left. Picking a heading scrolls
to it.

The viewer is the reader's child content and keeps its own parameters. The reader scrolls it and fills the height its
host gives it, so give it one, as with `height: 100%` in a box of a set height.

```razor
<div style="height: 80vh">
    <MarkdownReader @bind-OutlinePinned="pinned">
        <MarkdownViewer Markdown="@reply" ZoomImages="true" />
    </MarkdownReader>
</div>
```

Bind `OutlinePinned` to keep it pinned when the page renders again, and set `OutlineSide` to move the rail and the list
to the right. `OutlineLabel`, `PinOutlineLabel` and `UnpinOutlineLabel` translate the labels, and `--pmd-outline-bg` is
the list's background.

## MarkdownEditor

Edits Markdown as source in a native `<textarea>`, colored the way the viewer reads it, with a toolbar, line numbers,
format commands and list keys. It works under interactive server, WebAssembly and auto rendering. Put a `MarkdownViewer` beside it for a live
preview:

![The editor with its toolbar and line numbers, beside the viewer showing the same text, in a browser](https://raw.githubusercontent.com/polhem-dev/polhem-markdown/main/docs/images/editor-blazor.png)

```razor
<div style="display: grid; grid-template-columns: 1fr 1fr; height: 30rem">
    <MarkdownEditor @ref="editor" @bind-Markdown="text" aria-label="Markdown source" />
    <MarkdownViewer Markdown="@text" />
</div>

@code {
    private MarkdownEditor editor = default!;
    private string text = "# Hello";

    // Toggles bold on the selection, as Ctrl+B or Command+B does.
    private Task Bold() => editor.ApplyFormatAsync(MarkdownFormat.Bold);
}
```

The toolbar's labels are English; give your own, or `null` to keep one. Set `ShowToolbar="false"` to lay out buttons of
your own instead:

```razor
<MarkdownEditor @bind-Markdown="text" ToolbarLabels="Label" />

@code {
    private static string? Label(MarkdownToolbarItem item) => item switch
    {
        MarkdownToolbarItem.Bold => "粗體",
        MarkdownToolbarItem.Italic => "斜體",
        _ => null,
    };
}
```

The editor fills the height its host gives it, and loads its own script module. Keep `--pmd-code-font` monospaced: the
colors are drawn behind the textarea and must line up with it. The gallery in the repository keeps the editor and the
viewer scrolling together.

Each member's XML documentation has the details. The package is at 0.x: a minor version may still change the API. See
the [repository](https://github.com/polhem-dev/polhem-markdown).
