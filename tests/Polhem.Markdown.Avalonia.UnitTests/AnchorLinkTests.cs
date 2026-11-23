using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class AnchorLinkTests(HeadlessSession session)
{
    [Fact(DisplayName = "A link to a heading records the heading's id, and the heading knows its own id")]
    public Task Render_AnchorLink_RecordsHeadingId() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("Go to [setup](#setup).\n\n## Setup");
        var blocks = Blocks(viewer).Cast<MarkdownTextBlock>().ToList();

        var link = Assert.Single(blocks[0].Links);
        Assert.Equal("setup", link.AnchorId);
        Assert.Null(link.Target);
        Assert.Equal("Go to ".Length, link.Start);
        Assert.Equal(["setup"], Anchors(blocks[1]));
    });

    [Fact(DisplayName = "Following a link to a heading scrolls the heading to the top of the view")]
    public Task ScrollToHeading_ScrollsHeadingToTop() => session.RunAsync(() =>
    {
        var filler = string.Concat(Enumerable.Repeat("A paragraph that takes up some room.\n\n", 60));
        var viewer = new MarkdownViewer { Markdown = filler + "## Target\n\n" + filler };
        var scroller = new ScrollViewer { Content = viewer };
        var window = new Window { Width = 600, Height = 400, Content = scroller };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var heading = Descendants<MarkdownTextBlock>(viewer).Single(text => Anchors(text).Contains("target"));

        Assert.True(viewer.ScrollToAnchor("target"));
        Dispatcher.UIThread.RunJobs();

        var top = heading.TranslatePoint(new Point(0, 0), scroller)!.Value.Y;
        Assert.True(scroller.Offset.Y > 0);
        Assert.InRange(top, -1, 1);
    });

    [Fact(DisplayName = "Scrolling to an id no heading has does nothing")]
    public Task ScrollToHeading_UnknownId_ReturnsFalse() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("## Setup");

        Assert.False(viewer.ScrollToAnchor("nowhere"));
    });

    [Fact(DisplayName = "Text that links to a heading becomes a link once the heading arrives, though its own source did not change")]
    public Task Update_HeadingArrivesLater_RebuildsTheLinkingBlock() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("See [usage](#usage).\n\n## Usa");
        Assert.Empty(((MarkdownTextBlock)Blocks(viewer).First()).Links);

        viewer.Markdown = "See [usage](#usage).\n\n## Usage";

        Assert.Equal("usage", Assert.Single(((MarkdownTextBlock)Blocks(viewer).First()).Links).AnchorId);
    });

    [Fact(DisplayName = "A heading whose id changes because a heading of the same name arrives above it is rebuilt")]
    public Task Update_HeadingIdChanges_RebuildsTheHeading() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("intro\n\n## Notes");
        Assert.Equal(["notes"], Anchors(Blocks(viewer).Last()));

        viewer.Markdown = "## Notes\n\n## Notes";

        Assert.Equal(["notes", "notes-1"], Blocks(viewer).SelectMany(Anchors));
    });

    [Fact(DisplayName = "Blocks with no heading and no link to one are kept as before while a reply arrives")]
    public Task Update_PlainBlocks_AreKept() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("## Title\n\nfirst\n\nsec");
        var before = Blocks(viewer).ToList();

        viewer.Markdown = "## Title\n\nfirst\n\nsecond";

        Assert.Equal(1, viewer.LastBuiltBlockCount);
        Assert.Same(before[0], Blocks(viewer).First());
    });

    private static IReadOnlyList<string> Anchors(Control control) => control.GetValue(MarkdownViewer.AnchorIdsProperty);
}
