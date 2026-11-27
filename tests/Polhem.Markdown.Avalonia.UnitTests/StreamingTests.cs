using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>A reply shown while it arrives, its open syntax closed (polhem-dev/polhem-markdown#69).</summary>
[Collection(HeadlessCollection.Name)]
public class StreamingTests(HeadlessSession session)
{
    private static string LastText(MarkdownViewer viewer) => Descendants<MarkdownTextBlock>(viewer).Last().AccessibleText;

    [Fact(DisplayName = "While streaming, open emphasis is closed, so its markers do not show; once complete, the text shows as written")]
    public Task Streaming_ClosesEmphasis() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("");
        viewer.IsStreaming = true;
        viewer.Markdown = "Some **bold";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Some bold", LastText(viewer));
        Assert.Contains(Descendants<MarkdownTextBlock>(viewer).Last().Inlines!, inline => inline is Bold);

        viewer.IsStreaming = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Some **bold", LastText(viewer));
    });

    [Fact(DisplayName = "While streaming, an unfinished link shows as its text and cannot be followed")]
    public Task Streaming_UnfinishedLinkIsText() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("");
        viewer.IsStreaming = true;
        viewer.Markdown = "See [the docs](https://exa";
        Dispatcher.UIThread.RunJobs();

        var text = Descendants<MarkdownTextBlock>(viewer).Last();
        Assert.Equal("See the docs", text.AccessibleText);
        Assert.Empty(text.Links);

        viewer.Markdown = "See [the docs](https://example.org)";
        Dispatcher.UIThread.RunJobs();
        Assert.Single(Descendants<MarkdownTextBlock>(viewer).Last().Links);
    });

    [Fact(DisplayName = "While streaming, a table's header row shows as a table")]
    public Task Streaming_HeaderRowIsTable() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("");
        viewer.IsStreaming = true;
        viewer.Markdown = "| Name | Size";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["Name", "Size"], Descendants<MarkdownTextBlock>(viewer).Select(text => text.AccessibleText));
        Assert.Contains(Descendants<Grid>(viewer), grid => grid.GetValue(MarkdownViewer.TextGroupProperty) == MarkdownViewer.TextGroup.Table);
    });

    [Fact(DisplayName = "As the reply grows, only its last block is rebuilt")]
    public Task Streaming_RebuildsLastBlockOnly() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("");
        viewer.IsStreaming = true;
        viewer.Markdown = "# Title\n\nA first paragraph.\n\nA second **one";
        Dispatcher.UIThread.RunJobs();

        viewer.Markdown = "# Title\n\nA first paragraph.\n\nA second **one** that grows";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(1, viewer.LastBuiltBlockCount);
        Assert.Equal("A second one that grows", LastText(viewer));
    });

    [Theory(DisplayName = "While streaming, the last block after display math is completed; once complete, the text shows as written")]
    [InlineData("Read [site](ht", "Read site", "Read [site](ht")]
    [InlineData("Some **bold", "Some bold", "Some **bold")]
    public Task Streaming_CompletesAfterDisplayMath(string last, string streamed, string written) => session.RunAsync(() =>
    {
        var (_, viewer) = Show("");
        viewer.IsStreaming = true;
        viewer.Markdown = "A\n\n$$x = 1$$\n\n" + last;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(streamed, LastText(viewer));
        Assert.Empty(Descendants<MarkdownTextBlock>(viewer).Last().Links);

        viewer.IsStreaming = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(written, LastText(viewer));
    });

    [Fact(DisplayName = "While streaming, an unfinished image after display math waits")]
    public Task Streaming_ImageAfterDisplayMathWaits() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("");
        viewer.IsStreaming = true;
        viewer.Markdown = "A\n\n$$x = 1$$\n\n![Map](https://ex";
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain(Descendants<MarkdownTextBlock>(viewer), text => text.AccessibleText.Contains("![Map]", StringComparison.Ordinal));

        viewer.IsStreaming = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Contains(Descendants<MarkdownTextBlock>(viewer), text => text.AccessibleText.Contains("![Map](https://ex", StringComparison.Ordinal));
    });

    [Fact(DisplayName = "While streaming, a table's header row after display math shows as a table")]
    public Task Streaming_HeaderRowAfterDisplayMathIsTable() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("");
        viewer.IsStreaming = true;
        viewer.Markdown = "A\n\n$$x = 1$$\n\n| Name | Size";
        Dispatcher.UIThread.RunJobs();

        Assert.Contains(Descendants<Grid>(viewer), grid => grid.GetValue(MarkdownViewer.TextGroupProperty) == MarkdownViewer.TextGroup.Table);
    });
}
