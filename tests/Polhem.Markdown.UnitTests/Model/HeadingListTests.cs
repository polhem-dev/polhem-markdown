using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

/// <summary>The document's headings, for a table of contents (polhem-dev/polhem-markdown#71).</summary>
[Collection(ScalingCollection.Name)]
public class HeadingListTests
{
    [Fact(DisplayName = "The headings are listed in document order with their level, plain text, id and source offset")]
    public void Build_Headings_ListedInOrder()
    {
        const string markdown = "# Guide\n\nIntro.\n\n## Install **now**\n\n### `dotnet` tool\n\n## Install now";

        var headings = Parse(markdown).Headings;

        Assert.Equal(
            [
                new MarkdownHeading(1, "Guide", "guide", 0),
                new MarkdownHeading(2, "Install now", "install-now", markdown.IndexOf("## Install **", StringComparison.Ordinal)),
                new MarkdownHeading(3, "dotnet tool", "dotnet-tool", markdown.IndexOf("### ", StringComparison.Ordinal)),
                new MarkdownHeading(2, "Install now", "install-now-1", markdown.LastIndexOf("## ", StringComparison.Ordinal)),
            ],
            headings);
    }

    [Fact(DisplayName = "A heading's id in the list is the id of the heading the viewers render")]
    public void Build_HeadingIds_MatchRenderedHeadings()
    {
        var document = Parse("# A\n\n> ## Quoted\n\n- ## In a list\n\n# A");

        var rendered = document.Blocks.SelectMany(NodeTree.SelfAndDescendants).OfType<HeadingNode>().Select(heading => heading.Id);

        Assert.Equal(rendered, document.Headings.Select(heading => heading.Id));
        Assert.Equal(["a", "quoted", "in-a-list", "a-1"], document.Headings.Select(heading => heading.Id));
    }

    [Fact(DisplayName = "A heading without text has no id and is not listed")]
    public void Build_EmptyHeading_NotListed()
    {
        Assert.Equal(["after"], Parse("## !!!\n\n## After").Headings.Select(heading => heading.Id));
    }

    [Fact(DisplayName = "A text without headings lists none")]
    public void Build_NoHeadings_EmptyList()
    {
        Assert.Empty(Parse("Just a paragraph.").Headings);
    }
}
