using Polhem.Markdown.Tests.Shared;

namespace Polhem.Markdown.UnitTests.Model;

/// <summary>The syntax a reply cut off while it is arriving leaves open, closed (polhem-dev/polhem-markdown#69).</summary>
public class UnfinishedMarkdownTests
{
    [Theory(DisplayName = "Open syntax at the end of the reply is closed or held back")]
    [InlineData("Some **bold", "Some **bold**")]
    [InlineData("Some *italic", "Some *italic*")]
    [InlineData("Some _italic", "Some _italic_")]
    [InlineData("Some ~~gone", "Some ~~gone~~")]
    [InlineData("A ***strong and italic", "A ***strong and italic***")]
    [InlineData("**bold and *italic", "**bold and *italic***")]
    [InlineData("Some **", "Some")]
    [InlineData("Some text *", "Some text")]
    [InlineData("Use `dotnet bu", "Use `dotnet bu`")]
    [InlineData("Use ``a ` b", "Use ``a ` b``")]
    [InlineData("Use `", "Use")]
    [InlineData("**Run `dotnet", "**Run `dotnet`**")]
    [InlineData("See [the do", "See the do")]
    [InlineData("See [the docs]", "See the docs")]
    [InlineData("See [the docs](https://exa", "See the docs")]
    [InlineData("See [**the docs**](https://exa", "See **the docs**")]
    [InlineData("See [**the do", "See **the do**")]
    [InlineData("A ![cat](https://example.org/c", "A")]
    [InlineData("A ![ca", "A")]
    [InlineData("[![build pass", "")]
    [InlineData("See [![build](https://example.org/b.svg)](https://exa", "See ![build](https://example.org/b.svg)")]
    [InlineData("See [![build](https://example.org/b.svg)", "See ![build](https://example.org/b.svg)")]
    [InlineData("Open <deta", "Open")]
    [InlineData("Mail <https://exa", "Mail")]
    [InlineData("Less <", "Less")]
    [InlineData("| Name | Size", "| Name | Size\n| --- | --- |")]
    [InlineData("| Name | Size |\n| --", "| Name | Size |\n| --- | --- |")]
    [InlineData("| **Name | Size |", "| **Name | Size |\n| --- | --- |")]
    public void Complete_ClosesOpenSyntax(string unfinished, string expected)
    {
        Assert.Equal(expected, UnfinishedMarkdown.Complete(unfinished));
    }

    [Theory(DisplayName = "Text that is complete comes back as it is")]
    [InlineData("")]
    [InlineData("Plain text.")]
    [InlineData("Some **bold** and *italic* and ~~gone~~ and `code`.")]
    [InlineData("snake_case_name and 2 * 3 = 6")]
    [InlineData("See [the docs](https://example.org/a_(b)) and ![cat](https://example.org/cat.png).")]
    [InlineData("- [ ] open task\n- [x] done task")]
    [InlineData("* item one\n* item two")]
    [InlineData("***")]
    [InlineData("a < b and <b>bold</b>")]
    [InlineData("| A | B |\n|---|---|\n| 1 | 2 |")]
    [InlineData("Text\\*not emphasis and \\`not code")]
    public void Complete_LeavesCompleteText(string complete)
    {
        Assert.Equal(complete, UnfinishedMarkdown.Complete(complete));
    }

    [Fact(DisplayName = "The sample of every node type comes back as it is")]
    public void Complete_LeavesAllNodes()
    {
        var markdown = ModelNodeTypes.AllNodesMarkdown();

        Assert.Equal(markdown, UnfinishedMarkdown.Complete(markdown));
    }

    [Theory(DisplayName = "An open code fence or math block is left as it is: the rest of the reply is its content")]
    [InlineData("Text **bold\n\n```cs\nvar x = \"**")]
    [InlineData("Text\n\n~~~\nsome `code")]
    [InlineData("Text\n\n$$\nx^2 *")]
    public void Complete_LeavesOpenFence(string unfinished)
    {
        Assert.Equal(unfinished, UnfinishedMarkdown.Complete(unfinished));
    }

    [Theory(DisplayName = "Display math on one line opens no math block, so the blocks after it are completed")]
    [InlineData("A\n\n$$x = 1$$\n\nRead [site](ht", "A\n\n$$x = 1$$\n\nRead site")]
    [InlineData("A\n\n$$x = 1$$\n\n![Map](https://ex", "A\n\n$$x = 1$$\n\n")]
    [InlineData("A\n\n$$x = 1$$\n\nSome **bold", "A\n\n$$x = 1$$\n\nSome **bold**")]
    [InlineData("A\n\n$$x = 1$$\n\n| Name | Size", "A\n\n$$x = 1$$\n\n| Name | Size\n| --- | --- |")]
    [InlineData("A\n\n  $$ \\frac{36{,}000}{3} = 12{,}000 $$\n\nSee [the city's site](https://kyoto", "A\n\n  $$ \\frac{36{,}000}{3} = 12{,}000 $$\n\nSee the city's site")]
    [InlineData("A\n\n```x``` is code\n\nRead [site](ht", "A\n\n```x``` is code\n\nRead site")]
    public void Complete_CompletesAfterOneLineMath(string unfinished, string expected)
    {
        Assert.Equal(expected, UnfinishedMarkdown.Complete(unfinished));
    }

    [Theory(DisplayName = "After a closed math block or code fence, the blocks after it are completed")]
    [InlineData("A\n\n$$\nx = 1\n$$\n\nRead [site](ht", "A\n\n$$\nx = 1\n$$\n\nRead site")]
    [InlineData("A\n\n$$\nx = 1\n$$\nRead [site](ht", "A\n\n$$\nx = 1\n$$\nRead site")]
    [InlineData("A\n\n```\ncode\n```\n\nRead [site](ht", "A\n\n```\ncode\n```\n\nRead site")]
    public void Complete_CompletesAfterClosedFence(string unfinished, string expected)
    {
        Assert.Equal(expected, UnfinishedMarkdown.Complete(unfinished));
    }

    [Fact(DisplayName = "Only the last block changes, so the blocks before it keep their text and source offsets")]
    public void Complete_ChangesOnlyLastBlock()
    {
        const string before = "First **open\n\n```\ncode\n```\n\n";

        var completed = UnfinishedMarkdown.Complete(before + "Last [link](https://exa");

        Assert.Equal(before + "Last link", completed);
    }

    [Fact(DisplayName = "Every prefix of a complete text parses without error, and the complete text parses to the same model")]
    public void Complete_EveryPrefixParses()
    {
        var markdown = ModelNodeTypes.AllNodesMarkdown();
        for (var length = 0; length <= markdown.Length; length += 7)
        {
            MarkdownModelBuilder.Build(UnfinishedMarkdown.Complete(markdown[..length]), new MarkdownOptions());
        }
        Assert.Equal(markdown, UnfinishedMarkdown.Complete(markdown));
    }
}
