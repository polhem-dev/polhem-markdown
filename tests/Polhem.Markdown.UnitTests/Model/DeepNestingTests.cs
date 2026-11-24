using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

/// <summary>
/// Markdig nests some inline syntax as deep as the text asks. The model must stay shallow whatever the text, because the
/// conversion and every viewer walk it by recursion, and an overflow ends the process rather than failing the view.
/// </summary>
public class DeepNestingTests
{
    public static TheoryData<string, string> DeepTexts => new()
    {
        { "unmatched brackets before a link", new string('[', 10_000) + "](u)" },
        { "unmatched brackets", new string('[', 10_000) + "x" },
        { "unmatched image brackets", string.Concat(Enumerable.Repeat("![", 10_000)) + "x" },
        { "emphasis in emphasis", new string('*', 20_000) + "a" + new string('*', 20_000) },
    };

    [Theory(DisplayName = "Text Markdig nests thousands deep converts on a small stack into a shallow model")]
    [MemberData(nameof(DeepTexts))]
    public void Build_DeepNesting_IsShallow(string what, string markdown)
    {
        // NOTE: an overflow ends the process rather than failing the test, so the conversion runs on a thread with a
        // stack smaller than an iOS background thread's, where an overflow would come first.
        DocumentNode? document = null;
        var thread = new Thread(() => document = Parse(markdown), maxStackSize: 256 * 1024);
        thread.Start();
        thread.Join();

        Assert.NotNull(document);
        Assert.True(Depth(document) <= MarkdownModelBuilder.MaxInlineDepth + 2, what);
    }

    [Theory(DisplayName = "An unmatched bracket shows as written, as on GitHub")]
    [InlineData("a [b c")]
    [InlineData("[[x")]
    [InlineData("a ![b c")]
    [InlineData("x [ y [ z")]
    public void Build_UnmatchedBracket_ShowsAsWritten(string markdown)
    {
        Assert.Equal(markdown, Text(Single<ParagraphNode>(Parse(markdown)).Inlines));
    }

    [Fact(DisplayName = "An unmatched bracket inside a link's text shows as written, and the link is kept")]
    public void Build_UnmatchedBracketBeforeLink_KeepsLink()
    {
        var paragraph = Single<ParagraphNode>(Parse("a [b [c](https://example.com/) d"));

        Assert.Equal("a [b c d", Text(paragraph.Inlines));
        Assert.Equal("c", Text(Assert.Single(AllNodes(Parse("a [b [c](https://example.com/) d")).OfType<LinkNode>()).Inlines));
    }

    [Fact(DisplayName = "Emphasis nested past the limit keeps the outer levels and shows the rest as written")]
    public void Build_EmphasisPastTheLimit_ShowsRestAsWritten()
    {
        var levels = MarkdownModelBuilder.MaxInlineDepth + 8;
        var markdown = string.Concat(Enumerable.Repeat("*a ", levels)) + "b" + string.Concat(Enumerable.Repeat(" a*", levels));

        var document = Parse(markdown);

        Assert.Equal(MarkdownModelBuilder.MaxInlineDepth, AllNodes(document).OfType<EmphasisNode>().Count());
        var innermost = AllNodes(document).OfType<EmphasisNode>().Last();
        Assert.Contains(innermost.Inlines.OfType<TextNode>(), text => text.Text.StartsWith('*'));
    }

    private static int Depth(DocumentNode document)
    {
        var deepest = 0;
        var pending = new Stack<(MarkdownNode Node, int Depth)>(document.Blocks.Select(block => ((MarkdownNode)block, 1)));
        while (pending.TryPop(out var item))
        {
            deepest = Math.Max(deepest, item.Depth);
            IEnumerable<MarkdownNode> children = item.Node switch
            {
                ParagraphNode paragraph => paragraph.Inlines,
                EmphasisNode emphasis => emphasis.Inlines,
                LinkNode link => link.Inlines,
                AnchorLinkNode anchor => anchor.Inlines,
                _ => [],
            };
            foreach (var child in children)
            {
                pending.Push((child, item.Depth + 1));
            }
        }
        return deepest;
    }
}
