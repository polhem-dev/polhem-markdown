using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Polhem.Markdown.Tests.Shared;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class MarkdownViewerTests(HeadlessSession session)
{
    [Fact(DisplayName = "The viewer draws every node type of the model, so the front ends cannot drift apart")]
    public Task Render_AllNodesSample_DrawsEveryNodeType() => session.RunAsync(() =>
    {
        var (_, viewer) = Show(ModelNodeTypes.AllNodesMarkdown());

        Assert.Equal(ModelNodeTypes.All.OrderBy(type => type.Name), viewer.RenderedNodeTypes.OrderBy(type => type.Name));
    });

    [Fact(DisplayName = "No text, or null, shows nothing")]
    public Task Render_NullMarkdown_ShowsNothing() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("text");
        viewer.Markdown = null;

        Assert.Empty(Blocks(viewer));
    });

    [Fact(DisplayName = "A paragraph shows its text in selectable text")]
    public Task Render_Paragraph_ShowsSelectableText() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("Hello *world*");

        var text = Assert.IsAssignableFrom<SelectableTextBlock>(Assert.Single(Blocks(viewer)));
        Assert.Equal("Hello world", text.Inlines!.Text);
    });

    [Fact(DisplayName = "A heading is larger and heavier than body text")]
    public Task Render_Heading_IsLargerAndHeavier() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("# Title");

        var heading = Assert.IsAssignableFrom<TextBlock>(Assert.Single(Blocks(viewer)));
        Assert.True(heading.FontSize > viewer.GetValue(TextElement.FontSizeProperty));
        Assert.Equal(FontWeight.SemiBold, heading.FontWeight);
    });

    [Fact(DisplayName = "A link's character range matches where its text lies, even after a line break")]
    public Task Render_Link_RangeMatchesText() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("first line\\\nsee [the docs](https://example.com/docs) now");

        var text = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer)));
        var link = Assert.Single(text.Links);
        var expectedStart = text.Inlines!.Text!.IndexOf("the docs", StringComparison.Ordinal);
        Assert.Equal(expectedStart, link.Start);
        Assert.Equal("the docs".Length, link.Length);
        Assert.Equal(new Uri("https://example.com/docs"), text.LinkAt(expectedStart + 3));
        Assert.Null(text.LinkAt(expectedStart - 1));
    });

    [Fact(DisplayName = "A link with a refused scheme is shown as text that cannot be clicked")]
    public Task Render_RefusedLink_HasNoLinkRange() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("[click](javascript:alert(1))");

        var text = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer)));
        Assert.Empty(text.Links);
        Assert.Equal("click", text.Inlines!.Text);
    });

    [Fact(DisplayName = "A scheme the host allows becomes clickable, and changing the options rebuilds the view")]
    public Task Render_HostScheme_BecomesClickable() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("[Order](polhem:form/order)");
        Assert.Empty(Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer))).Links);

        var options = new MarkdownOptions();
        options.AllowedLinkSchemes.Add("polhem");
        viewer.Options = options;

        Assert.Equal("polhem", Assert.Single(Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer))).Links).Target.Scheme);
    });

    [Fact(DisplayName = "Activating a link raises LinkActivated, which bubbles to the window")]
    public Task ActivateLink_RaisesBubblingEvent() => session.RunAsync(() =>
    {
        var (window, viewer) = Show("[docs](https://example.com/)");
        var text = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer)));
        Uri? received = null;
        window.AddHandler(MarkdownViewer.LinkActivatedEvent, (_, e) =>
        {
            received = e.Uri;
            e.Handled = true;
        });

        viewer.ActivateLink(text, text.Links[0].Target);

        Assert.Equal(new Uri("https://example.com/"), received);
    });

    [Fact(DisplayName = "A code block shows its language and its code")]
    public Task Render_CodeBlock_ShowsLanguageAndCode() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```sql\nSELECT 1;\n```");

        var code = Assert.IsType<CodeBlockView>(Assert.Single(Blocks(viewer)));
        Assert.Equal("SELECT 1;", code.Code);
        Assert.Contains(Descendants<TextBlock>(code), text => text.Text == "sql");
        Assert.Contains(Descendants<SelectableTextBlock>(code), text => text.Text == "SELECT 1;");
    });

    [Fact(DisplayName = "The copy button of a code block puts the code on the clipboard")]
    public Task CopyCode_PutsCodeOnClipboard() => session.RunAsync(async () =>
    {
        var (window, viewer) = Show("```\nline 1\nline 2\n```");
        var code = Assert.IsType<CodeBlockView>(Assert.Single(Blocks(viewer)));

        await code.CopyAsync();

        Assert.Equal("line 1\nline 2", await window.Clipboard!.TryGetTextAsync());
    });

    [Fact(DisplayName = "The copy button carries the label the host sets, for screen readers and its tooltip")]
    public Task CopyCodeLabel_IsUsedByTheButton() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```\nx\n```");
        viewer.CopyCodeLabel = "複製";

        var code = Assert.IsType<CodeBlockView>(Assert.Single(Blocks(viewer)));
        Assert.Equal("複製", ToolTip.GetTip(code.CopyButton));
    });

    [Fact(DisplayName = "A table shows a cell for each column of each row, header included")]
    public Task Render_Table_ShowsEveryCell() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("| a | b |\n|---|--:|\n| 1 | 2 |\n| 3 |");

        var cells = Descendants<MarkdownTextBlock>(Assert.Single(Blocks(viewer))).ToList();
        Assert.Equal(["a", "b", "1", "2", "3", ""], cells.Select(cell => cell.Inlines!.Text ?? ""));
        Assert.Equal(TextAlignment.Right, cells[1].TextAlignment);
    });

    [Fact(DisplayName = "A numbered list keeps its numbering, and a bulleted list nested in it uses bullets")]
    public Task Render_Lists_ShowMarkers() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("3. three\n4. four\n   - nested");

        var markers = Descendants<TextBlock>(Assert.Single(Blocks(viewer)))
            .Where(text => text is not SelectableTextBlock)
            .Select(text => text.Text)
            .ToList();
        Assert.Equal(["3.", "4.", "◦"], markers);
    });

    [Fact(DisplayName = "Adding a block at the end builds only that block and keeps the others as they were")]
    public Task Update_Append_BuildsOnlyNewBlock() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("one\n\ntwo");
        var before = Blocks(viewer).ToList();

        viewer.Markdown = "one\n\ntwo\n\nthree";

        var after = Blocks(viewer).ToList();
        Assert.Equal(1, viewer.LastBuiltBlockCount);
        Assert.Same(before[0], after[0]);
        Assert.Same(before[1], after[1]);
        Assert.Equal(3, after.Count);
    });

    [Fact(DisplayName = "A block that grows while a reply arrives is rebuilt alone")]
    public Task Update_GrowingLastBlock_RebuildsOnlyIt() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("intro\n\npartial sen");
        var first = Blocks(viewer).First();

        viewer.Markdown = "intro\n\npartial sentence.";

        Assert.Equal(1, viewer.LastBuiltBlockCount);
        Assert.Same(first, Blocks(viewer).First());
        Assert.Equal("partial sentence.", ((SelectableTextBlock)Blocks(viewer).Last()).Inlines!.Text);
    });

    [Fact(DisplayName = "Removing blocks removes their controls")]
    public Task Update_Shrink_RemovesControls() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("one\n\ntwo\n\nthree");

        viewer.Markdown = "one";

        Assert.Single(Blocks(viewer));
        Assert.Equal(0, viewer.LastBuiltBlockCount);
    });

    [Fact(DisplayName = "Changing a color restyles the shown text without rebuilding it")]
    public Task LinkForeground_Change_AppliesWithoutRebuild() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("[docs](https://example.com/)");
        var text = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer)));
        var brush = new SolidColorBrush(Colors.Orange);

        viewer.LinkForeground = brush;

        Assert.Same(text, Assert.Single(Blocks(viewer)));
        var link = Assert.IsType<Span>(Assert.Single(text.Inlines!));
        Assert.Same(brush, link.Foreground);
    });

    [Fact(DisplayName = "Raw HTML is shown as text")]
    public Task Render_RawHtml_ShowsAsText() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("<script>alert(1)</script>");

        Assert.Equal("<script>alert(1)</script>", Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer))).Inlines!.Text);
    });
}
