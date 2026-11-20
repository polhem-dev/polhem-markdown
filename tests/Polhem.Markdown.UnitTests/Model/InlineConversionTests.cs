using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

public class InlineConversionTests
{
    [Theory(DisplayName = "Single delimiters make italic text and double delimiters bold text")]
    [InlineData("*word*", false)]
    [InlineData("_word_", false)]
    [InlineData("**word**", true)]
    [InlineData("__word__", true)]
    public void Build_Emphasis_SetsStrong(string markdown, bool strong)
    {
        var emphasis = Assert.IsType<EmphasisNode>(Assert.Single(Single<ParagraphNode>(Parse(markdown)).Inlines));

        Assert.Equal(strong, emphasis.IsStrong);
        Assert.Equal("word", Text(emphasis.Inlines));
    }

    [Fact(DisplayName = "Bold italic text is one emphasis nested in the other")]
    public void Build_BoldItalic_IsNested()
    {
        var outer = Assert.IsType<EmphasisNode>(Assert.Single(Single<ParagraphNode>(Parse("***word***")).Inlines));
        var inner = Assert.IsType<EmphasisNode>(Assert.Single(outer.Inlines));

        Assert.NotEqual(outer.IsStrong, inner.IsStrong);
        Assert.Equal("word", Text(inner.Inlines));
    }

    [Fact(DisplayName = "Bold text next to Chinese punctuation is still bold")]
    public void Build_CjkPunctuationEmphasis_IsStrong()
    {
        var paragraph = Single<ParagraphNode>(Parse("請注意**「必填」**欄位。"));

        var emphasis = Assert.Single(paragraph.Inlines.OfType<EmphasisNode>());
        Assert.True(emphasis.IsStrong);
        Assert.Equal("「必填」", Text(emphasis.Inlines));
    }

    [Fact(DisplayName = "Inline code keeps its text without the backticks")]
    public void Build_CodeSpan_KeepsCode()
    {
        var paragraph = Single<ParagraphNode>(Parse("Call `Save()` now."));

        Assert.Equal("Save()", Assert.Single(paragraph.Inlines.OfType<CodeSpanNode>()).Code);
    }

    [Theory(DisplayName = "Two trailing spaces or a backslash make a hard line break")]
    [InlineData("one  \ntwo")]
    [InlineData("one\\\ntwo")]
    public void Build_HardLineBreak_BecomesLineBreak(string markdown)
    {
        var paragraph = Single<ParagraphNode>(Parse(markdown));

        Assert.Single(paragraph.Inlines.OfType<LineBreakNode>());
        Assert.Equal("one\ntwo", Text(paragraph.Inlines));
    }

    [Fact(DisplayName = "A link keeps its target and its text")]
    public void Build_Link_KeepsTargetAndText()
    {
        var link = Assert.Single(Single<ParagraphNode>(Parse("[the **docs**](https://example.com/docs)")).Inlines.OfType<LinkNode>());

        Assert.Equal(new Uri("https://example.com/docs"), link.Target);
        Assert.Equal("the docs", Text(link.Inlines));
        Assert.Single(link.Inlines.OfType<EmphasisNode>());
    }

    [Theory(DisplayName = "An autolink, a bare URL and an email address become links")]
    [InlineData("<https://example.com/>", "https://example.com/", "https://example.com/")]
    [InlineData("see https://example.com/ here", "https://example.com/", "https://example.com/")]
    [InlineData("<someone@example.com>", "mailto:someone@example.com", "someone@example.com")]
    public void Build_Autolinks_BecomeLinks(string markdown, string target, string text)
    {
        var link = Assert.Single(Single<ParagraphNode>(Parse(markdown)).Inlines.OfType<LinkNode>());

        Assert.Equal(new Uri(target), link.Target);
        Assert.Equal(text, Text(link.Inlines));
    }

    [Fact(DisplayName = "A link with no text shows its target")]
    public void Build_LinkWithoutText_ShowsTarget()
    {
        var link = Assert.Single(Single<ParagraphNode>(Parse("[](https://example.com/)")).Inlines.OfType<LinkNode>());

        Assert.Equal("https://example.com/", Text(link.Inlines));
    }

    [Fact(DisplayName = "An HTML entity becomes the character it stands for")]
    public void Build_HtmlEntity_BecomesCharacter()
    {
        Assert.Equal("a & b © c", Text(Single<ParagraphNode>(Parse("a &amp; b &copy; c")).Inlines));
    }
}
