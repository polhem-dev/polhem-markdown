using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Bunit;
using Polhem.Markdown.Tests.Shared;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>
/// The HTML of <see cref="MarkdownHtml"/> next to what <see cref="MarkdownViewer"/> renders: the same markup, less the
/// parts a reader interacts with (polhem-dev/polhem-markdown#98).
/// </summary>
public class MarkdownHtmlTests : BunitContext
{
    private static IElement Parse(string fragment) =>
        new HtmlParser().ParseDocument("<!DOCTYPE html><html><body>" + fragment + "</body></html>").Body!.FirstElementChild!;

    [Fact(DisplayName = "The HTML is the markup the viewer renders, without its buttons and the attributes its script uses")]
    public void ToFragment_AllNodesSample_MatchesViewer()
    {
        var markdown = ModelNodeTypes.AllNodesMarkdown();
        var viewer = Render<MarkdownViewer>(parameters => parameters.Add(component => component.Markdown, markdown));
        var rendered = viewer.Find("div.pmd");

        // What the viewer has for a reader and its script, and the HTML does not: the button that copies code, the
        // focus the script moves to a heading or a note, and lazy loading.
        foreach (var button in rendered.QuerySelectorAll("button.pmd-copy").ToList())
        {
            button.Remove();
        }
        foreach (var element in rendered.QuerySelectorAll("*"))
        {
            foreach (var name in element.Attributes.Select(attribute => attribute.Name)
                .Where(name => name is "tabindex" or "loading" or "decoding" || name.StartsWith("blazor:", StringComparison.Ordinal)).ToList())
            {
                element.RemoveAttribute(name);
            }
        }

        // What the HTML has and the viewer does not: the ids a link moves to with no script.
        var exported = Parse(MarkdownHtml.ToFragment(markdown));
        foreach (var element in exported.QuerySelectorAll("[id]"))
        {
            element.RemoveAttribute("id");
        }

        rendered.MarkupMatches(exported.OuterHtml);
    }

    [Fact(DisplayName = "Math in the HTML is written with MathML elements and attributes only, whatever its text holds")]
    public void ToFragment_Math_WritesOnlyMathMl()
    {
        string[] elements =
        [
            "math", "semantics", "annotation", "mrow", "mi", "mn", "mo", "mtext", "msub", "msup", "msubsup", "munder",
            "mover", "munderover", "mfrac", "msqrt", "mroot", "mtable", "mtr", "mtd", "mspace",
        ];
        string[] attributes = ["display", "encoding", "mathvariant", "movablelimits", "largeop", "linethickness", "fence", "stretchy", "accent", "class", "width", "style"];
        var root = Parse(MarkdownHtml.ToFragment(ModelNodeTypes.AllNodesMarkdown()
            + "\n\n$\\text{\\\"><img src=x onerror=alert(1)> &amp; </math><script>}$ $x_{\\text{'\"}}$"
            + "\n\n$$\n\\operatorname{a<b>} \\begin{cases} \\text{<i>} & 1 \\end{cases} \\!\n$$"));

        var written = root.QuerySelectorAll("math").SelectMany(math => math.QuerySelectorAll("*").Prepend(math)).ToList();

        Assert.NotEmpty(written);
        Assert.All(written, element =>
        {
            Assert.Contains(element.LocalName, elements);
            Assert.Equal("http://www.w3.org/1998/Math/MathML", element.NamespaceUri);
            Assert.All(element.Attributes, attribute => Assert.Contains(attribute.Name, attributes));
        });
        Assert.Empty(root.QuerySelectorAll("script, img[src='x'], i"));
    }

    [Fact(DisplayName = "A diagram in the HTML is inline SVG with its text as content, and no script")]
    public void ToFragment_Diagram_IsInlineSvg()
    {
        var root = Parse(MarkdownHtml.ToFragment("```mermaid\nflowchart LR\n  A[\"<script>alert(1)</script>\"] -.->|go| B{Done?}\n```"));

        var svg = root.QuerySelector("div.pmd-diagram > svg")!;
        Assert.Equal("http://www.w3.org/2000/svg", svg.NamespaceUri);
        Assert.Contains("<script>alert(1)</script>", svg.QuerySelectorAll("text").Select(text => text.TextContent));
        Assert.Empty(root.QuerySelectorAll("script"));
    }
}
