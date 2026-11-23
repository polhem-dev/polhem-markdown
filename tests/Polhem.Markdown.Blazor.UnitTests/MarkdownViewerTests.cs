using Bunit;
using Microsoft.JSInterop;
using Polhem.Markdown.Tests.Shared;

namespace Polhem.Markdown.Blazor.UnitTests;

public class MarkdownViewerTests : BunitContext
{
    private const string ClipboardWrite = "navigator.clipboard.writeText";

    private IRenderedComponent<MarkdownViewer> Show(string? markdown, MarkdownOptions? options = null) =>
        Render<MarkdownViewer>(parameters => parameters
            .Add(viewer => viewer.Markdown, markdown)
            .Add(viewer => viewer.Options, options));

    [Fact(DisplayName = "The viewer draws every node type of the model, so the front ends cannot drift apart")]
    public void Render_AllNodesSample_DrawsEveryNodeType()
    {
        var viewer = Show(ModelNodeTypes.AllNodesMarkdown());

        Assert.Equal(ModelNodeTypes.All.OrderBy(type => type.Name), viewer.Instance.RenderedNodeTypes.OrderBy(type => type.Name));
    }

    [Fact(DisplayName = "The viewer draws every diagram shape type, so diagrams cannot drift apart between the front ends")]
    public void Render_AllNodesSample_DrawsEveryShapeType()
    {
        var viewer = Show(ModelNodeTypes.AllNodesMarkdown());

        Assert.Equal(ModelNodeTypes.Shapes.OrderBy(type => type.Name), viewer.Instance.RenderedShapeTypes.OrderBy(type => type.Name));
    }

    [Fact(DisplayName = "A diagram renders as inline SVG with its text as content, and no script")]
    public void Render_Diagram_IsInlineSvg()
    {
        var viewer = Show("```mermaid\nflowchart LR\n  A[\"<script>alert(1)</script>\"] -.->|go| B{Done?}\n```");

        var svg = viewer.Find("div.pmd-diagram > svg");
        Assert.StartsWith("0 0 ", svg.GetAttribute("viewBox"), StringComparison.Ordinal);
        var texts = svg.QuerySelectorAll("text").Select(text => text.TextContent).ToList();
        Assert.Contains("<script>alert(1)</script>", texts);
        Assert.Contains("go", texts);
        Assert.Empty(viewer.FindAll("script"));
        Assert.Equal("3", svg.QuerySelector("polyline")!.GetAttribute("stroke-dasharray"));
        Assert.Single(svg.QuerySelectorAll("polygon.pmd-d-arrow"));
        Assert.Single(svg.QuerySelectorAll("polygon.pmd-d-box"));
    }

    [Fact(DisplayName = "Diagram coordinates are written with a period, whatever the current culture")]
    public void Render_Diagram_InvariantNumbers()
    {
        var culture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var viewer = Show("```mermaid\nflowchart TD\n  A((Ring)) --> B\n```");

            Assert.DoesNotContain(",", viewer.Find("div.pmd-diagram > svg").GetAttribute("viewBox"), StringComparison.Ordinal);
            Assert.All(viewer.FindAll("text"), text => Assert.DoesNotContain(",", text.GetAttribute("x"), StringComparison.Ordinal));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact(DisplayName = "With diagrams turned off a mermaid block renders as code")]
    public void Render_DiagramsOff_RendersCode()
    {
        var viewer = Show("```mermaid\nflowchart TD\n  A --> B\n```", new MarkdownOptions { RenderMermaidDiagrams = false });

        viewer.Find("div.pmd-code");
        Assert.Empty(viewer.FindAll("div.pmd-diagram"));
    }

    [Theory(DisplayName = "No text, or null, renders an empty container")]
    [InlineData(null)]
    [InlineData("")]
    public void Render_NoText_RendersEmptyContainer(string? markdown)
    {
        Show(markdown).MarkupMatches("<div class=\"pmd\"></div>");
    }

    [Fact(DisplayName = "A paragraph with emphasis, inline code and a hard break renders as semantic HTML")]
    public void Render_Paragraph_IsSemanticHtml()
    {
        Show("Hello *big* **world** `code`  \nnext").MarkupMatches(
            "<div class=\"pmd\"><p>Hello <em>big</em> <strong>world</strong> <code>code</code><br>next</p></div>");
    }

    [Fact(DisplayName = "An alert renders a titled container whose class names its kind")]
    public void Render_Alert_HasKindClassAndTitle()
    {
        var viewer = Show("> [!TIP]\n> Use *this*.");

        var alert = viewer.Find("div.pmd-alert");
        Assert.Equal("pmd-alert pmd-alert-tip", alert.GetAttribute("class"));
        Assert.Equal("Tip", alert.QuerySelector("p.pmd-alert-title")!.TextContent);
        Assert.Equal("true", alert.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        Assert.Equal("Use this.", alert.QuerySelectorAll("p")[1].TextContent);
        Assert.Empty(viewer.FindAll("blockquote"));
    }

    [Fact(DisplayName = "Front matter is not rendered, and an emoji shortcode renders the emoji")]
    public void Render_FrontMatterAndEmoji()
    {
        Show("---\ntitle: x\n---\nDone :tada:").MarkupMatches("<div class=\"pmd\"><p>Done \U0001F389</p></div>");
    }

    [Fact(DisplayName = "Struck-through text renders as a del element")]
    public void Render_Strikethrough_IsDel()
    {
        Show("now ~~was~~").MarkupMatches("<div class=\"pmd\"><p>now <del>was</del></p></div>");
    }

    [Fact(DisplayName = "A task list item renders a disabled check box, and an ordinary item keeps its bullet")]
    public void Render_TaskList_HasDisabledCheckBoxes()
    {
        Show("- [x] done\n- [ ] open\n- plain").MarkupMatches(
            "<div class=\"pmd\"><ul>" +
            "<li class=\"pmd-task\"><input type=\"checkbox\" disabled checked><p>done</p></li>" +
            "<li class=\"pmd-task\"><input type=\"checkbox\" disabled><p>open</p></li>" +
            "<li><p>plain</p></li>" +
            "</ul></div>");
    }

    [Fact(DisplayName = "A heading keeps its level, and can take focus from a link to it")]
    public void Render_Heading_KeepsLevel()
    {
        Show("### Title").MarkupMatches("<div class=\"pmd\"><h3 tabindex=\"-1\">Title</h3></div>");
    }

    [Fact(DisplayName = "A numbered list keeps its start, and a loose list is marked so it gets more space")]
    public void Render_Lists_KeepStartAndLooseness()
    {
        var viewer = Show("3. three\n\n4. four");

        var list = viewer.Find("ol");
        Assert.Equal("3", list.GetAttribute("start"));
        Assert.Equal("pmd-loose", list.GetAttribute("class"));
        Assert.Equal(2, list.QuerySelectorAll("li").Length);
    }

    [Fact(DisplayName = "A code block shows its language and its code, and a copy button labelled for screen readers")]
    public void Render_CodeBlock_ShowsLanguageCodeAndButton()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(v => v.Markdown, "```sql\nSELECT 1;\n```")
            .Add(v => v.CopyCodeLabel, "複製"));

        Assert.Equal("sql", viewer.Find(".pmd-code-lang").TextContent);
        Assert.Equal("SELECT 1;", viewer.Find("pre code").TextContent);
        Assert.Equal("複製", viewer.Find("button.pmd-copy").GetAttribute("aria-label"));
        Assert.Equal("複製", viewer.Find("button.pmd-copy").GetAttribute("title"));
    }

    [Fact(DisplayName = "The copy button puts the code on the clipboard and shows a check mark")]
    public void CopyButton_Click_WritesClipboardAndShowsCheck()
    {
        var write = JSInterop.SetupVoid(ClipboardWrite, "line 1\nline 2");
        write.SetVoidResult();
        var viewer = Show("```\nline 1\nline 2\n```");
        var copyIcon = viewer.Find("button.pmd-copy path").GetAttribute("d");

        viewer.Find("button.pmd-copy").Click();

        Assert.Single(write.Invocations);
        viewer.WaitForAssertion(() => Assert.NotEqual(copyIcon, viewer.Find("button.pmd-copy path").GetAttribute("d")));
    }

    [Fact(DisplayName = "A clipboard the browser refuses leaves the view as it was")]
    public void CopyButton_ClipboardRefused_ShowsNoCheck()
    {
        JSInterop.SetupVoid(ClipboardWrite, _ => true).SetException(new JSException("The clipboard is not available."));
        var viewer = Show("```\nx\n```");
        var copyIcon = viewer.Find("button.pmd-copy path").GetAttribute("d");

        viewer.Find("button.pmd-copy").Click();

        Assert.Equal(copyIcon, viewer.Find("button.pmd-copy path").GetAttribute("d"));
    }

    [Fact(DisplayName = "A table puts header cells in thead, aligns columns, and fills missing cells")]
    public void Render_Table_HeaderAlignmentAndMissingCells()
    {
        var viewer = Show("| a | b |\n|---|--:|\n| 1 | 2 |\n| 3 |");

        Assert.Equal(["a", "b"], viewer.FindAll("thead th").Select(cell => cell.TextContent));
        Assert.Equal("text-align:right", viewer.FindAll("thead th")[1].GetAttribute("style"));
        Assert.Equal(["1", "2", "3", ""], viewer.FindAll("tbody td").Select(cell => cell.TextContent));
    }

    [Theory(DisplayName = "Raw HTML is encoded and shown as text, never added as markup")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("a <b>bold</b> word")]
    public void Render_RawHtml_IsEncoded(string markdown)
    {
        var viewer = Show(markdown);

        Assert.Equal(markdown, viewer.Find("p").TextContent);
        Assert.Empty(viewer.FindAll("p *"));
    }

    [Fact(DisplayName = "A link with a refused scheme renders no anchor")]
    public void Render_RefusedLink_HasNoAnchor()
    {
        var viewer = Show("[click](javascript:alert(1))");

        Assert.Empty(viewer.FindAll("a"));
        Assert.Equal("click", viewer.Find("p").TextContent);
    }

    [Fact(DisplayName = "Without a handler a link opens in a new tab, without access to this page")]
    public void Render_LinkWithoutHandler_OpensNewTab()
    {
        var link = Show("[docs](https://example.com/docs)").Find("a");

        Assert.Equal("https://example.com/docs", link.GetAttribute("href"));
        Assert.Equal("_blank", link.GetAttribute("target"));
        Assert.Equal("noopener noreferrer", link.GetAttribute("rel"));
    }

    [Fact(DisplayName = "With a handler a click reports the link to the host instead of navigating")]
    public void Click_LinkWithHandler_ReportsToHost()
    {
        var options = new MarkdownOptions();
        options.AllowedLinkSchemes.Add("polhem");
        Uri? received = null;
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(v => v.Markdown, "[Order](polhem:form/order?id=7)")
            .Add(v => v.Options, options)
            .Add(v => v.OnLinkActivated, (MarkdownLinkActivatedEventArgs e) => received = e.Uri));

        var link = viewer.Find("a");
        Assert.Null(link.GetAttribute("target"));
        link.Click();

        Assert.Equal(new Uri("polhem:form/order?id=7"), received);
    }

    [Fact(DisplayName = "Extra classes are added to the outer element")]
    public void Render_Class_IsAdded()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(v => v.Markdown, "x")
            .Add(v => v.Class, "reply"));

        Assert.Equal("pmd reply", viewer.Find("div").GetAttribute("class"));
    }

    [Fact(DisplayName = "A new text replaces the shown one")]
    public void Render_NewMarkdown_Replaces()
    {
        var viewer = Show("one");

        viewer.Render(parameters => parameters.Add(v => v.Markdown, "one\n\ntwo"));

        Assert.Equal(["one", "two"], viewer.FindAll("p").Select(p => p.TextContent));
    }
}
