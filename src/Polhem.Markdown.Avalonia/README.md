# Polhem.Markdown.Avalonia

A Markdown viewer control, a reader control that adds a table of contents around it, and a Markdown source editor
control for Avalonia. The viewer renders the same model as
`Polhem.Markdown.Blazor`, so an Avalonia and a Blazor front end show the same text the same way.

![The same reply in the Blazor viewer and the Avalonia viewer](https://raw.githubusercontent.com/polhem-dev/polhem-markdown/main/docs/images/viewer-hero.png)

## MarkdownViewer

Shows Markdown: GitHub Flavored Markdown, highlighted code, math, Mermaid diagrams and the HTML that README files use.
Raw HTML is never passed on, and only links and images with an allowed scheme open or load.

```xml
<Window xmlns:md="using:Polhem.Markdown.Avalonia">
  <md:MarkdownViewer Markdown="{Binding Reply}" LinkActivated="OnLinkActivated" />
</Window>
```

```csharp
// Without a handler that marks it handled, the viewer asks the operating system to open the link.
private void OnLinkActivated(object? sender, MarkdownLinkActivatedEventArgs e)
{
    OpenInApp(e.Uri);
    e.Handled = true;
}

// Resolve relative links and images against the document's address, and allow file images.
viewer.Options = new MarkdownOptions
{
    BaseUri = new Uri("https://example.com/docs/"),
    AllowedImageSchemes = { "file" },
};
```

Set `CjkFontFamily` when the text has Chinese, Japanese or Korean, so bold CJK text keeps its weight
([AvaloniaUI/Avalonia#18199](https://github.com/AvaloniaUI/Avalonia/issues/18199)):

```xml
<md:MarkdownViewer Markdown="{Binding Reply}"
                   CjkFontFamily="PingFang TC, Microsoft JhengHei UI, Noto Sans CJK TC" />
```

Text can be selected as in a browser: a drag runs from one block into the next, and scrolls the nearest scroll viewer
when it reaches its edge. Shift-click extends the selection, Ctrl+A (Cmd+A) selects the whole text, and Ctrl+C
(Cmd+C) copies it as plain text, with an empty line between blocks, a line break between list items and table rows,
and a tab between table cells. The text of a closed `<details>` section is left out.

`Headings` lists the text's headings for a table of contents, `HeadingsChanged` reports a change in them, and
`ScrollToHeading` scrolls one to the top of the view:

```csharp
outline.ItemsSource = viewer.Headings;   // each with Level, Text, Id and SourceOffset
viewer.HeadingsChanged += (_, _) => outline.ItemsSource = viewer.Headings;
outline.SelectionChanged += (_, _) =>
{
    if (outline.SelectedItem is MarkdownHeading heading)
    {
        viewer.ScrollToHeading(heading.Id);
    }
};
```

To mark the section being read, take the last heading whose `SourceOffset` is at or before `GetSourceOffset` at the
top of the view.

Or put the viewer in a `MarkdownReader`, below, which shows the table of contents itself.

To show a reply while it arrives, set the text again as it grows. Only the blocks that changed are rebuilt. Set
`IsStreaming` while it arrives, and the syntax its last block leaves open shows as it will once the reply is complete:
emphasis and code spans are closed, a link shows as its text until its address ends, an image or a tag still being
typed waits, and a table's header row shows as a table. Clear it when the reply ends, and the text shows exactly as
written.

```csharp
viewer.IsStreaming = true;
await foreach (var token in reply)
{
    text += token;
    viewer.Markdown = text;
}
viewer.IsStreaming = false;
```

Ctrl+F (⌘F on macOS) in the viewer opens a find bar at the top right of the view, which highlights every match and
goes through them with Enter, Shift+Enter, F3 and Shift+F3; Escape closes it. A match runs across emphasis and links,
and finds an image by its alternative text and a formula by its LaTeX. On a phone or a tablet, or from a host's own
toolbar or menu, open it with the API, which also finds without the bar. A `MarkdownReader` puts a search button at
the top of its rail.

```csharp
viewer.OpenFind();                    // the find bar, with the focus in it
var count = viewer.Find("order");     // highlights the matches and goes to the first
viewer.FindNext();                    // FindPrevious, FindMatchCount, CurrentFindMatch, CloseFind
```

`FindMatchBrush` and `FindCurrentMatchBrush` color the highlights, and `FindBarLabels` translates the bar's labels.

Set `ZoomImages` to open an image at full size over the window with a click or Enter, where the wheel, a pinch or
+ and - zoom it, a drag pans it, a double click switches between fitting the window and the image's own size, and
Escape or a click beside it closes it. An image inside a link opens the link instead. To show images your own way,
handle `ImageActivated`:

```csharp
viewer.ZoomImages = true;
viewer.ImageActivated += (_, e) =>
{
    ShowInMyViewer(e.Uri, e.AlternativeText);
    e.Handled = true;   // the viewer opens nothing
};
```

The open image has a toolbar at its top right with a close button and a save button, which writes the image's original
file where the user picks in a save dialog. To save it your own way, handle `ImageSaveRequested`:

```csharp
viewer.ImageSaveRequested += (_, e) =>
{
    SaveWithMyDownloader(e.Uri);
    e.Handled = true;   // the viewer shows no save dialog
};
```

Screen readers hear what the Blazor viewer's HTML tells them: headings with their level, links, lists and list items
with the task state, tables, images by their alternative text, and alerts and quotes as groups. Tab moves through the
links and buttons in the order of the text, passing over text without links, and Enter follows a link; following a
link to a footnote or a heading moves the focus there.

## MarkdownReader

A reading area for a viewer: it scrolls the viewer, keeps the text in a centred column (`TextMaxWidth`, 800 by
default), and offers a table of contents. A narrow rail along its edge draws a short line for each heading and marks
the section being read. Pointing at the rail, or clicking it, slides the list of headings out over the text; the pin in
the list keeps it beside the text as a column, the text centring in the room that is left. Picking a heading scrolls
to it.

The viewer is the reader's content and keeps its own properties; the reader scrolls it, so it needs no `ScrollViewer`
around it.

```xml
<md:MarkdownReader OutlinePinned="{Binding OutlinePinned}">
  <md:MarkdownViewer Markdown="{Binding Reply}" ZoomImages="True" />
</md:MarkdownReader>
```

`OutlinePinned` binds both ways, so a host can remember it, and `OutlineSide` moves the rail and the list to the right.
`OutlineLabel`, `PinOutlineLabel` and `UnpinOutlineLabel` translate the labels. `ScrollViewer` is the scroll viewer the
text scrolls in, for a host that keeps an editor in step with it.

## MarkdownEditor

Edits Markdown as source on AvaloniaEdit, colored the way the viewer reads it, with a toolbar, format commands and list
keys. Put a `MarkdownViewer` beside it for a live preview:

![The editor with its toolbar and line numbers, beside the viewer showing the same text](https://raw.githubusercontent.com/polhem-dev/polhem-markdown/main/docs/images/editor.png)

```xml
<Grid ColumnDefinitions="*,*">
  <md:MarkdownEditor x:Name="Editor" Markdown="{Binding Text}" />
  <md:MarkdownViewer Grid.Column="1" Markdown="{Binding Text}" />
</Grid>
```

```csharp
Editor.ApplyFormat(MarkdownFormat.Bold);   // toggles bold on the selection, as Ctrl+B or Cmd+B does
Editor.CycleHeading();                     // plain text, then #, ##, ###, then plain text again
```

A host that works on part of the text, such as a command that rewrites the selected paragraph, reads and sets the
selection and replaces ranges from code. Each `Replace` is one step to undo, and several ranges replaced together are one
step too, with their offsets in the text as it was before any of them:

```csharp
Editor.SelectionChanged += (_, _) => Status.Text = $"{Editor.SelectionLength} selected";
var paragraph = Editor.SelectedText;

Editor.Select(start, length);                 // selects a range and scrolls it into view
Editor.Replace(start, length, "new text");    // one step to undo
Editor.Replace(
[
    new MarkdownReplacement(0, 5, "Title"),
    new MarkdownReplacement(120, 0, "\n- added item"),
]);                                           // both in one step to undo
```

A host that shows something about parts of the text, such as a change it proposes, marks ranges with a background, an
underline or a strikethrough in a brush of its own. The marks move with the edits, a mark goes away when all of its text
is deleted, and an owner key clears one host's marks without touching others'. `GetTextBounds` tells where a range or an
offset is in the editor, or `null` while it is out of view, and `ViewChanged` tells when to ask again:

```csharp
var removed = Editor.AddMark(start, length, MarkdownRangeMarkStyle.Strikethrough, Brushes.Red, owner: this);
var added = Editor.AddMark(insertedAt, insertedLength, MarkdownRangeMarkStyle.Background, greenTint, owner: this);

Editor.ViewChanged += (_, _) =>
{
    // Place a popup under the added text, or hide it while that is scrolled out of view.
    if (Editor.GetTextBounds(added.Start, added.Length) is { } bounds)
    {
        Canvas.SetLeft(Popup, bounds.Left);
        Canvas.SetTop(Popup, bounds.Bottom);
    }
};

Editor.ClearMarks(this);    // this host's marks only
```

The gallery in the repository shows a change this way, with buttons to accept or reject it.

The toolbar's labels are English; give your own, or `null` to keep one. Set `ShowToolbar="False"` to lay out buttons of
your own instead:

```csharp
Editor.ToolbarLabels = item => item switch
{
    MarkdownToolbarItem.Bold => "粗體",
    MarkdownToolbarItem.Italic => "斜體",
    _ => null,
};
```

The editor brings the styles its AvaloniaEdit parts need, so it works under any theme. The gallery in the repository
keeps the editor and the viewer scrolling together.

Each member's XML documentation has the details. The package is at 0.x: a minor version may still change the API. See
the [repository](https://github.com/polhem-dev/polhem-markdown).
