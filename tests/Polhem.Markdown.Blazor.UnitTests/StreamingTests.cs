using Bunit;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>A reply shown while it arrives, its open syntax closed (polhem-dev/polhem-markdown#69).</summary>
public class StreamingTests : BunitContext
{
    private IRenderedComponent<MarkdownViewer> Stream(string markdown) =>
        Render<MarkdownViewer>(parameters => parameters
            .Add(viewer => viewer.Markdown, markdown)
            .Add(viewer => viewer.IsStreaming, true));

    [Fact(DisplayName = "While streaming, open emphasis is closed; once complete, the text shows as written")]
    public void Streaming_ClosesEmphasis()
    {
        var viewer = Stream("Some **bold");

        viewer.MarkupMatches("<div class=\"pmd\"><p>Some <strong>bold</strong></p></div>");

        viewer.Render(parameters => parameters.Add(component => component.IsStreaming, false));
        viewer.MarkupMatches("<div class=\"pmd\"><p>Some **bold</p></div>");
    }

    [Fact(DisplayName = "While streaming, an unfinished link shows as its text, with no link")]
    public void Streaming_UnfinishedLinkIsText()
    {
        var viewer = Stream("See [the docs](https://exa");

        viewer.MarkupMatches("<div class=\"pmd\"><p>See the docs</p></div>");
    }

    [Fact(DisplayName = "While streaming, an open code span is closed and an unfinished image waits")]
    public void Streaming_CodeSpanAndImage()
    {
        var viewer = Stream("Run `dotnet bu");
        viewer.MarkupMatches("<div class=\"pmd\"><p>Run <code>dotnet bu</code></p></div>");

        viewer.Render(parameters => parameters.Add(component => component.Markdown, "A ![cat](https://exa"));
        Assert.Empty(viewer.FindAll("img"));
    }

    [Fact(DisplayName = "While streaming, a table's header row shows as a table")]
    public void Streaming_HeaderRowIsTable()
    {
        var viewer = Stream("| Name | Size");

        Assert.Equal(["Name", "Size"], viewer.FindAll("th").Select(cell => cell.TextContent));
    }

    [Fact(DisplayName = "While streaming, an unfinished image after display math waits; once complete, the text shows as written")]
    public void Streaming_ImageAfterDisplayMathWaits()
    {
        var viewer = Stream("A\n\n$$x = 1$$\n\n![Map](https://ex");

        Assert.Empty(viewer.FindAll("img"));
        Assert.DoesNotContain("![Map]", viewer.Markup, StringComparison.Ordinal);

        viewer.Render(parameters => parameters.Add(component => component.IsStreaming, false));
        Assert.Contains("![Map](https://ex", viewer.Markup, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "While streaming, the last block after display math is completed")]
    [InlineData("Read [site](ht", "Read site")]
    [InlineData("Some **bold", "Some bold")]
    public void Streaming_CompletesAfterDisplayMath(string last, string text)
    {
        var viewer = Stream("A\n\n$$x = 1$$\n\n" + last);

        Assert.Equal(text, viewer.FindAll("p").Last().TextContent);
        Assert.Empty(viewer.FindAll("a"));
    }

    [Fact(DisplayName = "While streaming, a table's header row after display math shows as a table")]
    public void Streaming_HeaderRowAfterDisplayMathIsTable()
    {
        var viewer = Stream("A\n\n$$x = 1$$\n\n| Name | Size");

        Assert.Equal(["Name", "Size"], viewer.FindAll("th").Select(cell => cell.TextContent));
    }
}
