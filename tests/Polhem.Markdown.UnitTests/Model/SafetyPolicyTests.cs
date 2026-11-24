using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

/// <summary>
/// The safety policy applied while building the model: whatever a viewer receives is already safe to render, so no
/// viewer has to repeat these checks.
/// </summary>
public class SafetyPolicyTests
{
    [Theory(DisplayName = "Raw HTML never passes through: scripts and their content are dropped, and so are unsafe attributes")]
    [InlineData("<script>alert(1)</script>", "")]
    [InlineData("a <script>alert(1)</script> b", "a  b")]
    [InlineData("<style>p { color: red }</style>\n\nafter", "after")]
    [InlineData("<iframe src=\"https://example.com/\"></iframe>", "")]
    [InlineData("<span onclick=\"alert(1)\">text</span>", "text")]
    [InlineData("<!-- a comment -->", "")]
    public void Build_RawHtml_NeverPassesThrough(string markdown, string text)
    {
        var document = Parse(markdown);

        Assert.Equal(text, string.Concat(document.Blocks.OfType<ParagraphNode>().Select(paragraph => Text(paragraph.Inlines))));
        Assert.DoesNotContain(AllNodes(document), node => node is TextNode { Text: var shown } && shown.Contains('<', StringComparison.Ordinal));
    }

    [Theory(DisplayName = "An HTML link or image whose address is refused shows only its text")]
    [InlineData("<a href=\"javascript:alert(1)\">click</a>", "click")]
    [InlineData("<a href=\"JaVaScRiPt:alert(1)\">click</a>", "click")]
    [InlineData("<a href=\"data:text/html,x\">click</a>", "click")]
    [InlineData("<img src=\"javascript:alert(1)\" alt=\"pic\">", "pic")]
    [InlineData("<img src=x onerror=alert(1)>", "")]
    public void Build_RefusedHtmlTarget_ShowsText(string markdown, string text)
    {
        var document = Parse(markdown);

        Assert.DoesNotContain(AllNodes(document), node => node is LinkNode or ImageNode);
        Assert.Equal(text, string.Concat(document.Blocks.OfType<ParagraphNode>().Select(paragraph => Text(paragraph.Inlines))));
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

    [Fact(DisplayName = "An image from an allowed scheme is an image with its alternative text")]
    public void Build_Image_IsImage()
    {
        var image = Assert.IsType<ImageNode>(Assert.Single(Single<ParagraphNode>(Parse("![a red *pen*](https://example.com/pen.png \"Pen\")")).Inlines));

        Assert.Equal(new Uri("https://example.com/pen.png"), image.Source);
        Assert.Equal("a red pen", image.AlternativeText);
        Assert.Equal("Pen", image.Title);
    }

    [Theory(DisplayName = "An image from a scheme that is not allowed shows its alternative text and loads nothing")]
    [InlineData("http://example.com/pen.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("file:///etc/passwd")]
    [InlineData("pen.png")]
    [InlineData("/etc/passwd")]
    public void Build_ImageWithRefusedSource_ShowsAltText(string source)
    {
        var document = Parse($"![a red pen]({source})");

        Assert.Equal("a red pen", Text(Single<ParagraphNode>(document).Inlines));
        Assert.DoesNotContain(AllNodes(document), node => node is ImageNode or LinkNode);
    }

    [Fact(DisplayName = "With no image schemes allowed, every image shows its alternative text")]
    public void Build_NoImageSchemes_ShowsAltText()
    {
        var options = new MarkdownOptions();
        options.AllowedImageSchemes.Clear();

        Assert.DoesNotContain(AllNodes(Parse("![pen](https://example.com/pen.png)", options)), node => node is ImageNode);
    }

    [Fact(DisplayName = "An image's source resolves against the base, and the image schemes, not the link schemes, decide")]
    public void Build_RelativeImage_ResolvesAgainstBase()
    {
        var options = new MarkdownOptions { BaseUri = new Uri("file:///home/me/repo/README.md") };
        options.AllowedImageSchemes.Add("file");

        var image = Assert.Single(AllNodes(Parse("![logo](docs/logo.png)", options)).OfType<ImageNode>());

        Assert.Equal(new Uri("file:///home/me/repo/docs/logo.png"), image.Source);
        Assert.DoesNotContain("file", options.AllowedLinkSchemes);
    }

    [Fact(DisplayName = "An image inside a link is the link's content")]
    public void Build_ImageInLink_IsLinkContent()
    {
        var link = Assert.IsType<LinkNode>(Assert.Single(Single<ParagraphNode>(Parse("[![badge](https://img.example/b.svg)](https://ci.example/)")).Inlines));

        Assert.IsType<ImageNode>(Assert.Single(link.Inlines));
    }

    [Fact(DisplayName = "The text of a footnote goes through the same policy: a script link in it shows as text")]
    public void Build_FootnoteContent_FollowsPolicy()
    {
        var document = Parse("Text[^1]\n\n[^1]: [click](javascript:alert(1)) <a href=\"javascript:alert(1)\">html</a>");

        var note = Assert.Single(Assert.IsType<FootnotesNode>(document.Blocks[^1]).Notes);
        Assert.Equal("click html \u21A9", Text(Assert.IsType<ParagraphNode>(Assert.Single(note.Blocks)).Inlines));
        Assert.DoesNotContain(AllNodes(document), node => node is LinkNode);
    }

    [Fact(DisplayName = "The options are required")]
    public void Build_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => MarkdownModelBuilder.Build("text", null!));
    }
}
