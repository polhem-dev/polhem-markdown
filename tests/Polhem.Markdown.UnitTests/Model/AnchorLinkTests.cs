using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

public class AnchorLinkTests
{
    [Theory(DisplayName = "A heading gets the id GitHub gives it")]
    [InlineData("# Install", "install")]
    [InlineData("## Getting Started", "getting-started")]
    [InlineData("## What's new in v1.2?", "whats-new-in-v12")]
    [InlineData("## `MarkdownOptions` reference", "markdownoptions-reference")]
    [InlineData("## 安裝步驟", "安裝步驟")]
    [InlineData("## 1. First step", "1-first-step")]
    public void Build_Heading_HasGitHubId(string markdown, string id)
    {
        Assert.Equal(id, Single<HeadingNode>(Parse(markdown)).Id);
    }

    [Fact(DisplayName = "Headings with the same text get numbered ids, as on GitHub")]
    public void Build_RepeatedHeadings_AreNumbered()
    {
        var ids = Parse("## Notes\n\n## Notes\n\n## Notes").Blocks.OfType<HeadingNode>().Select(heading => heading.Id);

        Assert.Equal(["notes", "notes-1", "notes-2"], ids);
    }

    [Fact(DisplayName = "A repeat skips a numbered id a heading already took, as on GitHub")]
    public void Build_RepeatAfterNumberedHeading_SkipsTakenId()
    {
        var ids = Parse("# a-1\n\n# a\n\n# a\n\n# A!").Blocks.OfType<HeadingNode>().Select(heading => heading.Id);

        Assert.Equal(["a-1", "a", "a-2", "a-3"], ids);
    }

    [Fact(DisplayName = "Many headings of the same name parse in linear time")]
    public void Build_ManyRepeatedHeadings_ParseQuickly()
    {
        // Markdig's own numbering took over two seconds for this text; the slugger takes milliseconds.
        var markdown = string.Concat(Enumerable.Repeat("# a\n", 20_000));
        var watch = System.Diagnostics.Stopwatch.StartNew();

        var document = Parse(markdown);

        watch.Stop();
        Assert.Equal("a-19999", Assert.IsType<HeadingNode>(document.Blocks[^1]).Id);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"Parsing took {watch.Elapsed}.");
    }

    [Theory(DisplayName = "A heading with only punctuation or symbols gets an empty id")]
    [InlineData("## !!!")]
    [InlineData("## :tada:")]
    public void Build_SymbolHeading_HasNoId(string markdown)
    {
        Assert.Equal("", Single<HeadingNode>(Parse(markdown)).Id);
    }

    [Fact(DisplayName = "A link to a heading of the same text becomes an in-document link")]
    public void Build_LinkToHeading_IsAnchorLink()
    {
        var document = Parse("See [the setup](#install-the-package).\n\n## Install the package");

        var link = Assert.Single(AllNodes(document).OfType<AnchorLinkNode>());
        Assert.Equal("install-the-package", link.Id);
        Assert.Equal("the setup", Text(link.Inlines));
        Assert.DoesNotContain(AllNodes(document), node => node is LinkNode);
    }

    [Fact(DisplayName = "A link to a heading above it works as well as one to a heading below")]
    public void Build_LinkToEarlierHeading_IsAnchorLink()
    {
        var document = Parse("## Summary\n\nBack to [the summary](#summary).");

        Assert.Equal("summary", Assert.Single(AllNodes(document).OfType<AnchorLinkNode>()).Id);
    }

    [Fact(DisplayName = "A link to a heading with Chinese text works whether the id is written as is or percent-encoded")]
    public void Build_LinkToCjkHeading_AcceptsEncodedId()
    {
        var document = Parse("[直接](#安裝步驟) [編碼](#%E5%AE%89%E8%A3%9D%E6%AD%A5%E9%A9%9F)\n\n## 安裝步驟");

        Assert.All(AllNodes(document).OfType<AnchorLinkNode>(), link => Assert.Equal("安裝步驟", link.Id));
        Assert.Equal(2, AllNodes(document).OfType<AnchorLinkNode>().Count());
    }

    [Theory(DisplayName = "A link to an id no heading has shows as its text")]
    [InlineData("[missing](#nowhere)\n\n## Somewhere")]
    [InlineData("[case differs](#Install)\n\n## Install")]
    [InlineData("[just a hash](#)\n\n## Install")]
    public void Build_LinkToUnknownId_IsText(string markdown)
    {
        var document = Parse(markdown);

        Assert.DoesNotContain(AllNodes(document), node => node is AnchorLinkNode or LinkNode);
        Assert.Contains(AllNodes(document).OfType<TextNode>(), text => text.Text.Length > 0 && !text.Text.Contains('#', StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A link to a heading with no text of its own shows the target")]
    public void Build_EmptyLinkToHeading_ShowsTarget()
    {
        var link = Assert.Single(AllNodes(Parse("[](#install)\n\n## Install")).OfType<AnchorLinkNode>());

        Assert.Equal("#install", Text(link.Inlines));
    }

    [Fact(DisplayName = "A heading id does not turn a bracketed heading name into a link")]
    public void Build_HeadingName_IsNotImplicitLink()
    {
        var document = Parse("## Install\n\nSee [Install].");

        Assert.DoesNotContain(AllNodes(document), node => node is AnchorLinkNode or LinkNode);
    }

    [Fact(DisplayName = "A heading inside a list or a quote can be linked to")]
    public void Build_NestedHeading_CanBeLinked()
    {
        var document = Parse("[go](#deep)\n\n> - ### Deep");

        Assert.Equal("deep", Assert.Single(AllNodes(document).OfType<AnchorLinkNode>()).Id);
    }
}
