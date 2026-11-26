using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>The headings a host lists in a table of contents, and scrolls to (polhem-dev/polhem-markdown#71).</summary>
[Collection(HeadlessCollection.Name)]
public class HeadingListTests(HeadlessSession session)
{
    [Fact(DisplayName = "The viewer lists the text's headings, with the ids its headings carry")]
    public Task Headings_ListsHeadingsWithRenderedIds() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("# Guide\n\ntext\n\n## Install\n\n## Install");

        Assert.Equal(["guide", "install", "install-1"], viewer.Headings.Select(heading => heading.Id));
        Assert.Equal(
            viewer.Headings.Select(heading => heading.Id),
            Descendants<MarkdownTextBlock>(viewer).SelectMany(text => text.GetValue(MarkdownViewer.AnchorIdsProperty)));
    });

    [Fact(DisplayName = "A change of headings is reported once, and text that leaves the headings as they were is not")]
    public Task HeadingsChanged_RaisedOnlyWhenHeadingsChange() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("# Title\n\nA reply that");
        var changes = 0;
        viewer.HeadingsChanged += (_, _) => changes++;

        viewer.Markdown = "# Title\n\nA reply that grows";
        Assert.Equal(0, changes);

        viewer.Markdown = "# Title\n\nA reply that grows\n\n## Next";
        Assert.Equal(1, changes);
        Assert.Equal(["title", "next"], viewer.Headings.Select(heading => heading.Id));

        viewer.Markdown = "";
        Assert.Equal(2, changes);
        Assert.Empty(viewer.Headings);
    });

    [Fact(DisplayName = "Scrolling to a heading puts it at the top of the view")]
    public Task ScrollToHeading_ScrollsHeadingToTop() => session.RunAsync(() =>
    {
        var filler = string.Concat(Enumerable.Repeat("A paragraph that takes up some room.\n\n", 60));
        var viewer = new MarkdownViewer { ImageLoader = NoImages, Markdown = filler + "## Target\n\n" + filler };
        var scroller = new ScrollViewer { Content = viewer };
        var window = new Window { Width = 600, Height = 400, Content = scroller };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var heading = Descendants<MarkdownTextBlock>(viewer)
            .Single(text => text.GetValue(MarkdownViewer.AnchorIdsProperty).Contains("target"));

        Assert.True(viewer.ScrollToHeading("target"));
        Dispatcher.UIThread.RunJobs();

        Assert.True(scroller.Offset.Y > 0);
        Assert.InRange(heading.TranslatePoint(new Point(0, 0), scroller)!.Value.Y, -1, 1);
    });

    [Fact(DisplayName = "Scrolling back up to a heading also puts it at the top of the view")]
    public Task ScrollToHeading_Upwards_ScrollsHeadingToTop() => session.RunAsync(() =>
    {
        var filler = string.Concat(Enumerable.Repeat("A paragraph that takes up some room.\n\n", 60));
        var viewer = new MarkdownViewer { ImageLoader = NoImages, Markdown = filler + "## Target\n\n" + filler + "## End\n\n" + filler };
        var scroller = new ScrollViewer { Content = new Border { Padding = new Thickness(16, 8), Child = viewer } };
        var bar = new Border { Height = 50 };
        var panel = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        panel.Children.Add(bar);
        panel.Children.Add(scroller);
        var window = new Window { Width = 600, Height = 400, Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var heading = Descendants<MarkdownTextBlock>(viewer)
            .Single(text => text.GetValue(MarkdownViewer.AnchorIdsProperty).Contains("target"));
        Assert.True(viewer.ScrollToHeading("end"));
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewer.ScrollToHeading("target"));
        Dispatcher.UIThread.RunJobs();

        Assert.InRange(heading.TranslatePoint(new Point(0, 0), scroller)!.Value.Y, -1, 1);
    });

    [Fact(DisplayName = "Scrolling to an id no heading has, such as a footnote's, does nothing and returns false")]
    public Task ScrollToHeading_NotAHeading_ReturnsFalse() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("## Setup\n\nText[^1].\n\n[^1]: A note.");

        Assert.False(viewer.ScrollToHeading("nowhere"));
        Assert.True(viewer.ScrollToAnchor("fn:1"));
        Assert.False(viewer.ScrollToHeading("fn:1"));
    });
}
