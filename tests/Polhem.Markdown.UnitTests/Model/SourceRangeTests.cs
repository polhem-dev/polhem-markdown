using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

/// <summary>
/// Each node records where it came from in the source. A viewer compares those ranges with the source to rebuild only
/// the blocks that changed, and an editor maps the preview back to the source with them.
/// </summary>
public class SourceRangeTests
{
    [Fact(DisplayName = "Each top-level block covers its own source text")]
    public void Build_TopLevelBlocks_CoverTheirSource()
    {
        const string Markdown = "# Title\n\nFirst paragraph.\n\n- item\n\n```\ncode\n```";

        var slices = Parse(Markdown).Blocks.Select(block => Slice(Markdown, block.Range)).ToList();

        Assert.Equal(["# Title", "First paragraph.", "- item", "```\ncode\n```"], slices);
    }

    [Fact(DisplayName = "Inline nodes cover their own source text, delimiters included")]
    public void Build_Inlines_CoverTheirSource()
    {
        const string Markdown = "Say **bold** and `code` and [link](https://example.com/).";

        var inlines = Single<ParagraphNode>(Parse(Markdown)).Inlines;

        Assert.Equal("**bold**", Slice(Markdown, inlines.OfType<EmphasisNode>().Single().Range));
        Assert.Equal("`code`", Slice(Markdown, inlines.OfType<CodeSpanNode>().Single().Range));
        Assert.Equal("[link](https://example.com/)", Slice(Markdown, inlines.OfType<LinkNode>().Single().Range));
    }

    [Fact(DisplayName = "A range in a later block counts from the start of the whole text")]
    public void Build_NestedNode_RangeIsAbsolute()
    {
        const string Markdown = "intro\n\n> quoted *word*";

        var quote = Assert.IsType<QuoteNode>(Parse(Markdown).Blocks[1]);
        var emphasis = Assert.IsType<ParagraphNode>(Assert.Single(quote.Blocks)).Inlines.OfType<EmphasisNode>().Single();

        Assert.Equal("*word*", Slice(Markdown, emphasis.Range));
    }

    [Fact(DisplayName = "Every range of every node lies within the source")]
    public void Build_AllRanges_LieWithinSource()
    {
        const string Markdown = "# H\n\n| a | b |\n|---|---|\n| 1 | 2 |\n\n1. one\n2. two  \n   three\n\n> q\n\n---\n\n<b>x</b> &amp; ![i](https://e/i.png)";

        Assert.All(AllNodes(Parse(Markdown)), node =>
        {
            Assert.InRange(node.Range.Start, 0, Markdown.Length);
            Assert.InRange(node.Range.End, node.Range.Start, Markdown.Length);
        });
    }

    private static string Slice(string markdown, SourceRange range) => markdown.Substring(range.Start, range.Length);
}
