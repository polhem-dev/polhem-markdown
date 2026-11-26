using Bunit;

namespace Polhem.Markdown.Blazor.UnitTests;

public class IncrementalRenderTests : BunitContext
{
    private IRenderedComponent<MarkdownViewer> Show(string markdown) =>
        Render<MarkdownViewer>(parameters => parameters.Add(viewer => viewer.Markdown, markdown));

    /// <summary>Sets the text and returns how many top-level blocks rendered for it.</summary>
    private static int Change(IRenderedComponent<MarkdownViewer> viewer, string markdown)
    {
        var before = viewer.Instance.BlockRenderCount;
        viewer.Render(parameters => parameters.Add(component => component.Markdown, markdown));
        return viewer.Instance.BlockRenderCount - before;
    }

    private static string[] Paragraphs(IRenderedComponent<MarkdownViewer> viewer) =>
        [.. viewer.FindAll("div.pmd > p").Select(paragraph => paragraph.TextContent)];

    [Fact(DisplayName = "Each top-level block renders once when the viewer first shows the text")]
    public void FirstRender_RendersEveryBlockOnce()
    {
        var viewer = Show("one\n\ntwo\n\nthree");

        Assert.Equal(3, viewer.Instance.BlockRenderCount);
    }

    [Fact(DisplayName = "A block added at the end renders only it")]
    public void Append_RendersOnlyNewBlock()
    {
        var viewer = Show("one\n\ntwo");

        Assert.Equal(1, Change(viewer, "one\n\ntwo\n\nthree"));
        Assert.Equal(["one", "two", "three"], Paragraphs(viewer));
    }

    [Fact(DisplayName = "A block inserted before others renders only it, and the ones after it move down")]
    public void InsertAtStart_RendersOnlyNewBlock()
    {
        var viewer = Show("one\n\ntwo\n\nthree");

        Assert.Equal(1, Change(viewer, "zero\n\none\n\ntwo\n\nthree"));
        Assert.Equal(["zero", "one", "two", "three"], Paragraphs(viewer));
    }

    [Fact(DisplayName = "Removing a block from the middle renders nothing and keeps the blocks on both sides")]
    public void RemoveFromMiddle_RendersNothing()
    {
        var viewer = Show("one\n\ntwo\n\nthree\n\nfour");

        Assert.Equal(0, Change(viewer, "one\n\nthree\n\nfour"));
        Assert.Equal(["one", "three", "four"], Paragraphs(viewer));
    }

    [Fact(DisplayName = "Replacing blocks in the middle with more renders only the new ones")]
    public void ReplaceMiddle_RendersOnlyNewBlocks()
    {
        var viewer = Show("one\n\ntwo\n\nthree\n\nfour");

        Assert.Equal(3, Change(viewer, "one\n\nA\n\nB\n\nC\n\nfour"));
        Assert.Equal(["one", "A", "B", "C", "four"], Paragraphs(viewer));
    }

    [Fact(DisplayName = "Blocks with the same source render side by side, and one more renders only it")]
    public void SameSource_RendersEachOnce()
    {
        var viewer = Show("same\n\n---\n\nsame\n\n---");

        Assert.Equal(1, Change(viewer, "same\n\n---\n\nsame\n\n---\n\nsame"));
        Assert.Equal(["same", "same", "same"], Paragraphs(viewer));
        Assert.Equal(2, viewer.FindAll("div.pmd > hr").Count);
    }

    [Fact(DisplayName = "A kept block whose footnote number changed renders again with the new number")]
    public void FootnoteRenumbered_RendersReferenceAgain()
    {
        var viewer = Show("b[^b]\n\n[^a]: A\n[^b]: B");
        Assert.Equal("1", viewer.Find("sup.pmd-fnref a").TextContent);

        Change(viewer, "a[^a]\n\nb[^b]\n\n[^a]: A\n[^b]: B");

        Assert.Equal(["1", "2"], viewer.FindAll("sup.pmd-fnref a").Select(link => link.TextContent));
    }

    [Fact(DisplayName = "A heading added after a link to it turns the kept paragraph into a link")]
    public void HeadingArrives_RendersLinkAgain()
    {
        var viewer = Show("[usage](#usage)");
        Assert.Empty(viewer.FindAll("a"));

        Change(viewer, "[usage](#usage)\n\n## Usage");

        Assert.Single(viewer.FindAll("p a"));
    }

    [Fact(DisplayName = "Changing a setting that every block shows renders every block again")]
    public void CopyCodeLabel_Change_RendersEveryBlock()
    {
        var viewer = Show("text\n\n```\ncode\n```");
        var before = viewer.Instance.BlockRenderCount;

        viewer.Render(parameters => parameters.Add(component => component.CopyCodeLabel, "複製"));

        Assert.Equal(2, viewer.Instance.BlockRenderCount - before);
        Assert.Equal("複製", viewer.Find("button.pmd-copy").GetAttribute("aria-label"));
    }

    [Fact(DisplayName = "The check mark of the copy button shows in a code block kept from an earlier text")]
    public void CopyButton_KeptBlock_ShowsCheck()
    {
        JSInterop.SetupVoid("navigator.clipboard.writeText", "code").SetVoidResult();
        var viewer = Show("```\ncode\n```\n\nfirst");
        var copyIcon = viewer.Find("button.pmd-copy path").GetAttribute("d");
        Assert.Equal(1, Change(viewer, "```\ncode\n```\n\nsecond"));
        var before = viewer.Instance.BlockRenderCount;

        viewer.Find("button.pmd-copy").Click();

        viewer.WaitForAssertion(() => Assert.NotEqual(copyIcon, viewer.Find("button.pmd-copy path").GetAttribute("d")));
        Assert.Equal(1, viewer.Instance.BlockRenderCount - before);
    }

    [Fact(DisplayName = "A streaming reply whose last block is closed before parsing renders only that block as it grows")]
    public void Streaming_ClosedLastBlock_RendersOnlyIt()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(component => component.Markdown, "one\n\n**bo")
            .Add(component => component.IsStreaming, true));

        Assert.Equal(1, Change(viewer, "one\n\n**bold"));
        Assert.Equal("bold", viewer.Find("div.pmd > p strong").TextContent);
    }
}
