using Bunit;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>The elements of raw HTML the model takes over, as the Blazor viewer writes them.</summary>
public class HtmlTests : BunitContext
{
    private IRenderedComponent<MarkdownViewer> Show(string markdown) =>
        Render<MarkdownViewer>(parameters => parameters.Add(viewer => viewer.Markdown, markdown));

    [Fact(DisplayName = "A centered or right-aligned block is a division with a class for its alignment")]
    public void Render_AlignedBlock_HasAlignmentClass()
    {
        Show("<p align=\"center\">centered</p>\n\n<div align=\"right\">right</div>").MarkupMatches(
            "<div class=\"pmd\"><div class=\"pmd-align-center\"><p>centered</p></div><div class=\"pmd-align-right\"><p>right</p></div></div>");
    }

    [Fact(DisplayName = "A details section is a details element with its summary, open only when the text says so")]
    public void Render_Details_IsDetailsElement()
    {
        Show("<details><summary>More <b>info</b></summary>\n\nHidden.\n\n</details>\n\n<details open>\n\nOpen\n\n</details>").MarkupMatches(
            "<div class=\"pmd\">"
            + "<details class=\"pmd-details\"><summary>More <strong>info</strong></summary><p>Hidden.</p></details>"
            + "<details class=\"pmd-details\" open><summary>Details</summary><p>Open</p></details>"
            + "</div>");
    }

    [Fact(DisplayName = "Superscript, subscript and keys are written as their elements")]
    public void Render_SupSubKbd_AreElements()
    {
        Show("x<sup>2</sup> H<sub>2</sub>O <kbd>Ctrl</kbd>").MarkupMatches(
            "<div class=\"pmd\"><p>x<sup>2</sup> H<sub>2</sub>O <kbd>Ctrl</kbd></p></div>");
    }

    [Fact(DisplayName = "An image keeps the width and height written in HTML")]
    public void Render_ImageSize_IsKept()
    {
        var image = Show("<img src=\"https://example.com/a.png\" alt=\"A\" width=\"120\" height=\"40\">").Find("img");

        Assert.Equal("120", image.GetAttribute("width"));
        Assert.Equal("40", image.GetAttribute("height"));
        Assert.Equal("max-height:40px", image.GetAttribute("style"));
    }

    [Fact(DisplayName = "A picture with a source for each theme is written as a picture the browser picks from")]
    public void Render_Picture_IsPicture()
    {
        var viewer = Show("""
            <picture>
              <source media="(prefers-color-scheme: dark)" srcset="https://example.com/dark.png">
              <source media="(prefers-color-scheme: light)" srcset="javascript:alert(1)">
              <img alt="Logo" src="https://example.com/light.png">
            </picture>
            """);

        var source = Assert.Single(viewer.FindAll("picture > source"));
        Assert.Equal("(prefers-color-scheme: dark)", source.GetAttribute("media"));
        Assert.Equal("https://example.com/dark.png", source.GetAttribute("srcset"));
        Assert.Equal("https://example.com/light.png", viewer.Find("picture > img").GetAttribute("src"));
    }
}
