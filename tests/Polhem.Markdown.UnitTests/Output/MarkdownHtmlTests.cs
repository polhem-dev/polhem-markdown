using System.Text;
using Polhem.Markdown.Output;
using Polhem.Markdown.Tests.Shared;

namespace Polhem.Markdown.UnitTests.Output;

/// <summary>The HTML <see cref="MarkdownHtml"/> writes (polhem-dev/polhem-markdown#98).</summary>
public class MarkdownHtmlTests
{
    private static HtmlWriter WriteAllNodes()
    {
        var writer = new HtmlWriter(new StringBuilder(), new MarkdownHtmlOptions());
        writer.Document(MarkdownModelBuilder.Build(ModelNodeTypes.AllNodesMarkdown(), new MarkdownOptions()));
        return writer;
    }

    [Fact(DisplayName = "The HTML has every node type of the model, so it cannot drift apart from the viewers")]
    public void ToFragment_AllNodesSample_WritesEveryNodeType()
    {
        Assert.Equal(ModelNodeTypes.All.OrderBy(type => type.Name), WriteAllNodes().WrittenNodeTypes.OrderBy(type => type.Name));
    }

    [Fact(DisplayName = "The HTML has every diagram shape type")]
    public void ToFragment_AllNodesSample_WritesEveryShapeType()
    {
        Assert.Equal(ModelNodeTypes.Shapes.OrderBy(type => type.Name), WriteAllNodes().WrittenShapeTypes.OrderBy(type => type.Name));
    }

    [Fact(DisplayName = "The HTML has every formula part type")]
    public void ToFragment_AllNodesSample_WritesEveryFormulaType()
    {
        Assert.Equal(ModelNodeTypes.Formulas.OrderBy(type => type.Name), WriteAllNodes().WrittenFormulaTypes.OrderBy(type => type.Name));
    }

    [Fact(DisplayName = "A fragment is one div with the viewer's class, and has no script, no style and no button")]
    public void ToFragment_AllNodesSample_IsStatic()
    {
        var html = MarkdownHtml.ToFragment(ModelNodeTypes.AllNodesMarkdown());

        Assert.StartsWith("<div class=\"pmd\">", html, StringComparison.Ordinal);
        Assert.EndsWith("</div>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<style", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<button", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(" on", html.Replace(" one", "", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Raw HTML and text that looks like markup come out as text")]
    public void ToFragment_Markup_IsText()
    {
        var html = MarkdownHtml.ToFragment("<script>alert(1)</script>\n\nA <img src=x onerror=alert(1)> and `<b>` and \"quotes\" & more");

        Assert.DoesNotContain("<script", html, StringComparison.Ordinal);
        Assert.DoesNotContain("onerror", html, StringComparison.Ordinal);
        Assert.Contains("<code>&lt;b&gt;</code>", html, StringComparison.Ordinal);
        Assert.Contains("&quot;quotes&quot; &amp; more", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A link with a scheme that is not allowed is its text, and an allowed one opens in a new page")]
    public void ToFragment_Links_FollowThePolicy()
    {
        Assert.Equal("<div class=\"pmd\"><p>click</p></div>", MarkdownHtml.ToFragment("[click](javascript:alert(1))"));
        Assert.Equal(
            "<div class=\"pmd\"><p><a href=\"https://example.com/?a=1&amp;b=%22\" target=\"_blank\" rel=\"noopener noreferrer\">docs</a></p></div>",
            MarkdownHtml.ToFragment("[docs](https://example.com/?a=1&b=\")"));
    }

    [Fact(DisplayName = "A link to a heading is an ordinary #id link, and the heading carries the id")]
    public void ToFragment_AnchorLink_PointsAtHeadingId()
    {
        Assert.Equal(
            "<div class=\"pmd\"><p><a href=\"#setup\">setup</a></p><h2 id=\"setup\">Setup</h2></div>",
            MarkdownHtml.ToFragment("[setup](#setup)\n\n## Setup"));
    }

    [Fact(DisplayName = "A footnote reference and its back link point at each other's ids")]
    public void ToFragment_Footnotes_LinkBothWays()
    {
        var html = MarkdownHtml.ToFragment("Text[^n].\n\n[^n]: The note.");

        Assert.Contains("<sup class=\"pmd-fnref\"><a id=\"fnref:1\" href=\"#fn:1\">1</a></sup>", html, StringComparison.Ordinal);
        Assert.Contains("<li id=\"fn:1\"><p>The note. <a class=\"pmd-fnback\" href=\"#fnref:1\" aria-label=\"Back to reference\">↩</a></p></li>", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Diagram coordinates are written with a period, whatever the current culture")]
    public void ToFragment_Diagram_InvariantNumbers()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var html = MarkdownHtml.ToFragment("```mermaid\nflowchart LR\n  A[One and a half] --> B\n```");

            Assert.Contains("<svg viewBox=\"0 0 ", html, StringComparison.Ordinal);
            Assert.DoesNotMatch("(x|y|width|height)=\"[0-9]+,[0-9]", html);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact(DisplayName = "Images stay links, with no lazy loading, so a page printed at once has them all")]
    public void ToFragment_Image_IsLink()
    {
        Assert.Equal(
            "<div class=\"pmd\"><p><img class=\"pmd-img\" src=\"https://example.com/a.png\" alt=\"a &quot;cat&quot;\" title=\"t\" referrerpolicy=\"no-referrer\"></p></div>",
            MarkdownHtml.ToFragment("![a \"cat\"](https://example.com/a.png \"t\")"));
    }

    [Fact(DisplayName = "An image the host returns the bytes of is written as a data URI, and each source is asked for once")]
    public void ToFragment_ImageContent_IsDataUri()
    {
        var asked = new List<Uri>();
        var options = new MarkdownHtmlOptions
        {
            ImageContent = source =>
            {
                asked.Add(source);
                return source.AbsolutePath == "/a.png" ? new MarkdownImageContent("image/png", new byte[] { 1, 2, 3 }) : null;
            },
        };

        var html = MarkdownHtml.ToFragment(
            "![a](https://example.com/a.png) ![again](https://example.com/a.png) ![b](https://example.com/b.png)", htmlOptions: options);

        Assert.Equal(2, Count(html, "src=\"data:image/png;base64,AQID\""));
        Assert.Contains("src=\"https://example.com/b.png\"", html, StringComparison.Ordinal);
        Assert.Equal([new Uri("https://example.com/a.png"), new Uri("https://example.com/b.png")], asked);
    }

    [Fact(DisplayName = "The host is never asked for an image the policy refused")]
    public void ToFragment_ImageContent_OnlyForAllowedSources()
    {
        var asked = new List<Uri>();
        var options = new MarkdownHtmlOptions { ImageContent = source => { asked.Add(source); return null; } };

        var html = MarkdownHtml.ToFragment("![local](file:///etc/passwd) ![remote](http://example.com/a.png)", htmlOptions: options);

        Assert.Empty(asked);
        Assert.DoesNotContain("<img", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "The sources of a picture for a dark and a light theme are written, and embedded too")]
    public void ToFragment_Picture_EmbedsEverySource()
    {
        var options = new MarkdownHtmlOptions { ImageContent = _ => new MarkdownImageContent("image/svg+xml", new byte[] { 0 }) };

        var html = MarkdownHtml.ToFragment(
            "<picture><source media=\"(prefers-color-scheme: dark)\" srcset=\"https://example.com/d.png\"><img src=\"https://example.com/l.png\" alt=\"logo\"></picture>",
            htmlOptions: options);

        Assert.Contains("<picture><source media=\"(prefers-color-scheme: dark)\" srcset=\"data:image/svg+xml;base64,AA==\"><img class=\"pmd-img\" src=\"data:image/svg+xml;base64,AA==\"", html, StringComparison.Ordinal);
    }

    [Theory(DisplayName = "Image content accepts only an image media type with no parameters")]
    [InlineData("image/png", true)]
    [InlineData("image/svg+xml", true)]
    [InlineData("IMAGE/JPEG", true)]
    [InlineData("image/", false)]
    [InlineData("text/html", false)]
    [InlineData("image/png;charset=x", false)]
    [InlineData("image/png,\"><script>", false)]
    [InlineData("image/png ", false)]
    public void MarkdownImageContent_MediaType(string mediaType, bool accepted)
    {
        var create = () => new MarkdownImageContent(mediaType, ReadOnlyMemory<byte>.Empty);

        if (accepted)
        {
            Assert.Equal(mediaType, create().MediaType);
        }
        else
        {
            Assert.Throws<ArgumentException>(create);
        }
    }

    [Fact(DisplayName = "A page has the stylesheet, a title from the first heading, and the fragment in its body")]
    public void ToPage_Defaults()
    {
        var page = MarkdownHtml.ToPage("# A < B & C\n\nText");

        Assert.StartsWith("<!DOCTYPE html>\n<html>\n<head>\n<meta charset=\"utf-8\">", page, StringComparison.Ordinal);
        Assert.Contains("<title>A &lt; B &amp; C</title>", page, StringComparison.Ordinal);
        Assert.Contains(MarkdownHtml.StyleSheet, page, StringComparison.Ordinal);
        Assert.Contains("<body>\n" + MarkdownHtml.ToFragment("# A < B & C\n\nText") + "\n</body>", page, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A page takes the title, language and extra style the host gives")]
    public void ToPage_Options()
    {
        var page = MarkdownHtml.ToPage("Text", htmlOptions: new MarkdownHtmlOptions
        {
            Title = "Notes \"1\"",
            Language = "zh-TW",
            StyleSheet = ".pmd { --pmd-link: red; }",
        });

        Assert.StartsWith("<!DOCTYPE html>\n<html lang=\"zh-TW\">", page, StringComparison.Ordinal);
        Assert.Contains("<title>Notes &quot;1&quot;</title>", page, StringComparison.Ordinal);
        Assert.True(page.IndexOf(".pmd { --pmd-link: red; }", StringComparison.Ordinal) > page.IndexOf(MarkdownHtml.StyleSheet, StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A page without a heading or a title has no title element")]
    public void ToPage_NoHeading_NoTitle()
    {
        Assert.DoesNotContain("<title>", MarkdownHtml.ToPage("Text"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Extra style that would end the style element is refused")]
    public void ToPage_StyleSheetEndingStyle_Throws()
    {
        var options = new MarkdownHtmlOptions { StyleSheet = "a{}</STYLE><script>alert(1)</script>" };

        Assert.Throws<ArgumentException>(() => MarkdownHtml.ToPage("Text", htmlOptions: options));
    }

    [Fact(DisplayName = "The stylesheet keeps every viewer rule and the variables, and drops the interactive parts and comments")]
    public void StyleSheet_KeepsTheViewerRules()
    {
        var css = File.ReadAllText(Path.Combine(RepositoryPaths.SourceDirectory(), "Polhem.Markdown.Blazor", "wwwroot", "polhem-markdown.css"));
        var sheet = MarkdownHtml.StyleSheet;

        // The filter reads flat rules; an at-rule or a nested rule in the viewer's file would need it to change.
        Assert.DoesNotContain("@", css, StringComparison.Ordinal);
        Assert.Equal(Count(css, "{"), Count(css, "}"));

        foreach (var kept in new[] { ".pmd {", ".pmd .pmd-alert-note {", ".pmd .pmd-diagram .pmd-d-box {", ":is(.pmd, .pmd-editor) .pmd-tk-keyword {", ".pmd math {", ".pmd .pmd-task > svg {" })
        {
            Assert.Contains(kept, sheet, StringComparison.Ordinal);
        }
        foreach (var dropped in new[] { ".pmd-editor >", ".pmd-zoom", ".pmd-reader", "::highlight", ".pmd-copy", ".pmd-image-button", "/*" })
        {
            Assert.DoesNotContain(dropped, sheet, StringComparison.Ordinal);
        }
        Assert.Contains("var(--pmd-link, #3b82f6)", sheet, StringComparison.Ordinal);
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }
}
