using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

/// <summary>
/// The safety policy applied while building the model: whatever a viewer receives is already safe to render, so no
/// viewer has to repeat these checks.
/// </summary>
public class SafetyPolicyTests
{
    [Theory(DisplayName = "Raw HTML, as a block or inline, shows as the text it was written as")]
    [InlineData("<div>block</div>", "<div>block</div>")]
    [InlineData("<script>alert(1)</script>", "<script>alert(1)</script>")]
    [InlineData("a <b>bold</b> word", "a <b>bold</b> word")]
    [InlineData("<img src=x onerror=alert(1)>", "<img src=x onerror=alert(1)>")]
    public void Build_RawHtml_ShowsAsText(string markdown, string text)
    {
        var document = Parse(markdown);

        Assert.Equal(text, Text(Single<ParagraphNode>(document).Inlines));
        Assert.DoesNotContain(AllNodes(document), node => node is LinkNode);
    }

    [Theory(DisplayName = "A link that is not an absolute URI with an allowed scheme shows as its text")]
    [InlineData("[click](javascript:alert(1))")]
    [InlineData("[click](JavaScript:alert(1))")]
    [InlineData("[click](data:text/html,<b>x</b>)")]
    [InlineData("[click](vbscript:msgbox)")]
    [InlineData("[click](/etc/passwd)")]
    [InlineData("[click](file:///etc/passwd)")]
    [InlineData("[click](relative/page)")]
    [InlineData("[click](#anchor)")]
    [InlineData("<javascript:alert(1)>")]
    public void Build_DisallowedLink_ShowsAsText(string markdown)
    {
        var document = Parse(markdown);

        Assert.DoesNotContain(AllNodes(document), node => node is LinkNode);
        Assert.NotEmpty(Text(Single<ParagraphNode>(document).Inlines));
    }

    [Fact(DisplayName = "A refused link keeps the formatting of its text")]
    public void Build_DisallowedLink_KeepsFormatting()
    {
        var paragraph = Single<ParagraphNode>(Parse("[a **bold** link](javascript:void)"));

        Assert.Equal("a bold link", Text(paragraph.Inlines));
        Assert.Single(paragraph.Inlines.OfType<EmphasisNode>());
    }

    [Fact(DisplayName = "The schemes of the defaults are matched without regard to case")]
    public void Build_UppercaseScheme_IsAllowed()
    {
        var link = Assert.Single(Single<ParagraphNode>(Parse("[x](HTTPS://example.com/)")).Inlines.OfType<LinkNode>());

        Assert.Equal("https", link.Target.Scheme);
    }

    [Fact(DisplayName = "A scheme the host adds to the options makes its links clickable")]
    public void Build_HostScheme_IsAllowedWhenAdded()
    {
        const string Markdown = "[Order](polhem:form/order?id=7)";
        var options = new MarkdownOptions();
        options.AllowedLinkSchemes.Add("polhem");

        Assert.DoesNotContain(AllNodes(Parse(Markdown)), node => node is LinkNode);
        var link = Assert.Single(Single<ParagraphNode>(Parse(Markdown, options)).Inlines.OfType<LinkNode>());
        Assert.Equal("polhem", link.Target.Scheme);
    }

    [Fact(DisplayName = "A scheme the host removes from the options is no longer clickable")]
    public void Build_RemovedScheme_IsRefused()
    {
        var options = new MarkdownOptions();
        options.AllowedLinkSchemes.Remove("mailto");

        Assert.DoesNotContain(AllNodes(Parse("<someone@example.com>", options)), node => node is LinkNode);
    }

    [Fact(DisplayName = "An image shows as its alternative text and loads nothing")]
    public void Build_Image_ShowsAltText()
    {
        var document = Parse("![a red *pen*](https://example.com/pen.png)");

        Assert.Equal("a red pen", Text(Single<ParagraphNode>(document).Inlines));
        Assert.DoesNotContain(AllNodes(document), node => node is LinkNode);
    }

    [Fact(DisplayName = "A footnote reference whose definition is not a URL shows as its label, without a link")]
    public void Build_Footnote_ShowsLabelWithoutLink()
    {
        // Without the footnote extension, `[^1]: note` is a link reference definition and `[^1]` a link to the
        // relative target `note`, which the policy refuses. The definition is not shown.
        var document = Parse("Text[^1]\n\n[^1]: note");

        Assert.Equal("Text^1", Text(Single<ParagraphNode>(document).Inlines));
        Assert.DoesNotContain(AllNodes(document), node => node is LinkNode);
    }

    [Fact(DisplayName = "The options are required")]
    public void Build_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MarkdownModelBuilder.Build("text", null!));
    }
}
