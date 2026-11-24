using Bunit;

namespace Polhem.Markdown.Blazor.UnitTests;

public class ImageTests : BunitContext
{
    private IRenderedComponent<MarkdownViewer> Show(string markdown, MarkdownOptions? options = null) =>
        Render<MarkdownViewer>(parameters => parameters
            .Add(viewer => viewer.Markdown, markdown)
            .Add(viewer => viewer.Options, options));

    [Fact(DisplayName = "An image is an img that loads lazily, sends no referrer and keeps its alternative text and title")]
    public void Render_Image_IsImg()
    {
        var viewer = Show("![red pen](https://example.com/pen.png \"Pen\")");

        viewer.MarkupMatches(
            "<div class=\"pmd\"><p><img class=\"pmd-img\" src=\"https://example.com/pen.png\" alt=\"red pen\" title=\"Pen\" "
            + "loading=\"lazy\" decoding=\"async\" referrerpolicy=\"no-referrer\"></p></div>");
    }

    [Fact(DisplayName = "An SVG image is an img like any other, where its scripts cannot run")]
    public void Render_SvgImage_IsImg()
    {
        var viewer = Show("![build passing](https://img.example/badge.svg)");

        Assert.Equal("https://img.example/badge.svg", viewer.Find("img").GetAttribute("src"));
        Assert.Empty(viewer.FindAll("svg"));
    }

    [Theory(DisplayName = "An image from a scheme that is not allowed is its alternative text, with no img")]
    [InlineData("http://example.com/pen.png")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:image/svg+xml;base64,PHN2ZyBvbmxvYWQ9YWxlcnQoMSk+")]
    public void Render_RefusedImage_IsText(string source)
    {
        var viewer = Show($"![red pen]({source})");

        Assert.Empty(viewer.FindAll("img"));
        Assert.Equal("red pen", viewer.Find("p").TextContent);
    }

    [Fact(DisplayName = "A relative image resolves against the base")]
    public void Render_RelativeImage_ResolvesAgainstBase()
    {
        var viewer = Show("![logo](docs/logo.png)", new MarkdownOptions { BaseUri = new Uri("https://example.com/repo/README.md") });

        Assert.Equal("https://example.com/repo/docs/logo.png", viewer.Find("img").GetAttribute("src"));
    }

    [Fact(DisplayName = "An image inside a link is inside the a element")]
    public void Render_ImageInLink_IsInsideLink()
    {
        var viewer = Show("[![badge](https://img.example/b.svg)](https://ci.example/)");

        Assert.Equal("https://img.example/b.svg", viewer.Find("a > img").GetAttribute("src"));
    }
}
