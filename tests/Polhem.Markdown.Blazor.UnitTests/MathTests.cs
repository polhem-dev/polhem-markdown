using Bunit;
using Polhem.Markdown.Tests.Shared;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>Math, which the Blazor viewer writes as MathML for the browser to lay out.</summary>
public class MathTests : BunitContext
{
    private IRenderedComponent<MarkdownViewer> Show(string markdown) =>
        Render<MarkdownViewer>(parameters => parameters.Add(viewer => viewer.Markdown, markdown));

    [Fact(DisplayName = "Inline math is MathML in the line, with its LaTeX as an annotation")]
    public void Render_InlineMath_IsMathMl()
    {
        Show("Area $\\pi r^2$.").MarkupMatches(
            "<div class=\"pmd\"><p>Area <math><semantics><mrow><mi>π</mi><msup><mi>r</mi><mn>2</mn></msup></mrow>"
            + "<annotation encoding=\"application/x-tex\">\\pi r^2</annotation></semantics></math>.</p></div>");
    }

    [Fact(DisplayName = "Display math is a block of MathML shown on its own")]
    public void Render_DisplayMath_IsBlock()
    {
        var viewer = Show("$$\n\\frac{a}{b}\n$$");

        var math = viewer.Find("div.pmd-math > math");
        Assert.Equal("block", math.GetAttribute("display"));
        Assert.NotNull(math.QuerySelector("mfrac"));
    }

    [Fact(DisplayName = "Large operators carry their limits below and above, which the browser moves aside within a line")]
    public void Render_LargeOperator_HasLimits()
    {
        var viewer = Show("$$\\sum_{i=1}^{n} i$$");

        var operatorElement = viewer.Find("munderover > mo");
        Assert.Equal("true", operatorElement.GetAttribute("largeop"));
        Assert.Equal("true", operatorElement.GetAttribute("movablelimits"));
    }

    [Fact(DisplayName = "Delimiters stretch, a matrix is a table, and cases align left")]
    public void Render_FencedMatrix_IsTable()
    {
        var viewer = Show("$\\begin{cases} 1 & x > 0 \\\\ 0 & \\text{else} \\end{cases}$");

        Assert.Equal("true", viewer.Find("mrow > mo[fence]").GetAttribute("stretchy"));
        Assert.Equal(4, viewer.FindAll("mtable mtd.pmd-math-left").Count);
        Assert.Equal("else", viewer.Find("mtext").TextContent);
    }

    [Fact(DisplayName = "A bracket written on its own keeps its size, and text keeps its spaces")]
    public void Render_PlainBracket_DoesNotStretch()
    {
        var viewer = Show("$f(x) \\text{ if } x$");

        Assert.All(viewer.FindAll("mo").Where(mo => mo.TextContent is "(" or ")"), mo => Assert.Equal("false", mo.GetAttribute("stretchy")));
        Assert.Equal("\u00A0if\u00A0", viewer.Find("mtext").TextContent);
    }

    [Fact(DisplayName = "Math outside the supported subset shows its source as code")]
    public void Render_UnsupportedMath_IsCode()
    {
        Show("$\\mathbf{x}$").MarkupMatches("<div class=\"pmd\"><p><code>\\mathbf{x}</code></p></div>");
    }

    [Fact(DisplayName = "Math is written with MathML elements and attributes only, whatever its text holds")]
    public void Render_Math_WritesOnlyMathMl()
    {
        string[] elements =
        [
            "math", "semantics", "annotation", "mrow", "mi", "mn", "mo", "mtext", "msub", "msup", "msubsup", "munder",
            "mover", "munderover", "mfrac", "msqrt", "mroot", "mtable", "mtr", "mtd", "mspace",
        ];
        string[] attributes = ["display", "encoding", "mathvariant", "movablelimits", "largeop", "linethickness", "fence", "stretchy", "accent", "class", "width", "style"];
        var viewer = Show(ModelNodeTypes.AllNodesMarkdown()
            + "\n\n$\\text{\\\"><img src=x onerror=alert(1)> &amp; </math><script>}$ $x_{\\text{'\"}}$"
            + "\n\n$$\n\\operatorname{a<b>} \\begin{cases} \\text{<i>} & 1 \\end{cases} \\!\n$$");

        var written = viewer.FindAll("math").SelectMany(math => math.QuerySelectorAll("*").Prepend(math)).ToList();

        Assert.NotEmpty(written);
        Assert.All(written, element =>
        {
            Assert.Contains(element.LocalName, elements);
            Assert.Equal("http://www.w3.org/1998/Math/MathML", element.NamespaceUri);
            Assert.All(element.Attributes, attribute => Assert.Contains(attribute.Name, attributes));
        });
        Assert.Empty(viewer.FindAll("script, img[src='x'], i"));
    }

    [Fact(DisplayName = "LaTeX that looks like markup is text in the MathML, never markup")]
    public void Render_MarkupInMath_IsText()
    {
        var viewer = Show("$\\text{<script>alert(1)</script>}$");

        Assert.Equal("<script>alert(1)</script>", viewer.Find("mtext").TextContent);
        Assert.Empty(viewer.FindAll("script"));
    }
}
