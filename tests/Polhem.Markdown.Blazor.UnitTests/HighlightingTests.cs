using Bunit;

namespace Polhem.Markdown.Blazor.UnitTests;

public class HighlightingTests : BunitContext
{
    private IRenderedComponent<MarkdownViewer> Show(string markdown) =>
        Render<MarkdownViewer>(parameters => parameters.Add(viewer => viewer.Markdown, markdown));

    [Fact(DisplayName = "Highlighted code is text with each token in a span classed by its kind")]
    public void Render_HighlightedCode_SpansTokens()
    {
        var viewer = Show("```cs\nvar s = \"x\"; // c\n```");

        viewer.Find("pre code").MarkupMatches(
            "<code><span class=\"pmd-tk-keyword\">var</span> s = <span class=\"pmd-tk-string\">\"x\"</span>; "
            + "<span class=\"pmd-tk-comment\">// c</span></code>");
    }

    [Fact(DisplayName = "Code in a language the highlighter does not know stays plain text")]
    public void Render_UnknownLanguage_IsPlain()
    {
        var viewer = Show("```rust\nfn main() {}\n```");

        viewer.Find("pre code").MarkupMatches("<code>fn main() {}</code>");
    }

    [Fact(DisplayName = "Markup in code is text, never markup, whether highlighted or not")]
    public void Render_MarkupInCode_IsText()
    {
        var viewer = Show("```html\n<script>alert(1)</script>\n```");

        Assert.Empty(viewer.FindAll("pre script"));
        Assert.Equal("<script>alert(1)</script>", viewer.Find("pre code").TextContent);
    }
}
