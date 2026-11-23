using Bunit;
using Microsoft.AspNetCore.Components;

namespace Polhem.Markdown.Blazor.UnitTests;

public class FootnoteTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown.js";

    private readonly BunitJSModuleInterop _module;

    public FootnoteTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        _module.SetupVoid("scrollToAnchor", _ => true).SetVoidResult();
    }

    private IRenderedComponent<MarkdownViewer> Show(string markdown) =>
        Render<MarkdownViewer>(parameters => parameters.Add(viewer => viewer.Markdown, markdown));

    private List<string> ScrolledIds() => _module.Invocations["scrollToAnchor"]
        .Select(invocation => Assert.IsType<ElementReference>(invocation.Arguments[0]).Id)
        .ToList();

    [Fact(DisplayName = "A reference is a superscript link, and the notes are a numbered list after the text with back links")]
    public void Render_Footnotes_Markup()
    {
        var viewer = Show("Text[^n].\n\n[^n]: The note.");

        viewer.MarkupMatches(
            "<div class=\"pmd\"><p>Text<sup class=\"pmd-fnref\"><a href=\"#fn:1\">1</a></sup>.</p>"
            + "<section class=\"pmd-footnotes\"><ol><li tabindex=\"-1\"><p>The note. "
            + "<a class=\"pmd-fnback\" href=\"#fnref:1\" aria-label=\"Back to reference\">↩</a></p></li></ol></section></div>");
    }

    [Fact(DisplayName = "A note referenced twice gets a numbered second back link")]
    public void Render_RepeatedReference_NumbersBackLinks()
    {
        var viewer = Show("One[^n], two[^n].\n\n[^n]: The note.");

        Assert.Equal(["↩", "↩2"], viewer.FindAll("a.pmd-fnback").Select(link => link.TextContent));
        Assert.Equal("Back to reference 2", viewer.FindAll("a.pmd-fnback")[1].GetAttribute("aria-label"));
        Assert.All(viewer.FindAll("sup a"), link => Assert.Equal("1", link.TextContent));
    }

    [Fact(DisplayName = "A reference moves to its note, and the back link moves to that reference")]
    public void Click_ReferenceAndBackLink_MoveBothWays()
    {
        var viewer = Show("One[^a], two[^b].\n\n[^a]: Note A.\n\n[^b]: Note B.");

        viewer.FindAll("sup a")[1].Click();
        viewer.FindAll("a.pmd-fnback")[1].Click();
        viewer.FindAll("sup a")[1].Click();

        var ids = ScrolledIds();
        Assert.Equal(3, ids.Count);
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.NotEqual(ids[0], ids[1]);
        Assert.Equal(ids[0], ids[2]);
    }

    [Fact(DisplayName = "Footnote links are handled by the viewer and never reported to the host")]
    public void Click_FootnoteLinks_DoNotRaiseOnLinkActivated()
    {
        var reported = false;
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(component => component.Markdown, "Text[^n].\n\n[^n]: The note.")
            .Add(component => component.OnLinkActivated, _ => reported = true));

        viewer.Find("sup a").Click();
        viewer.Find("a.pmd-fnback").Click();

        Assert.False(reported);
    }

    [Fact(DisplayName = "A reference added before others renumbers them, and their links still reach the right notes")]
    public void Update_EarlierReference_RenumbersAndStillNavigates()
    {
        const string Notes = "\n\n[^a]: A.\n\n[^b]: B.";
        var viewer = Show("Intro.\n\nLater[^b]." + Notes);

        viewer.Render(parameters => parameters.Add(component => component.Markdown, "Intro[^a].\n\nLater[^b]." + Notes));
        viewer.FindAll("sup a")[1].Click();

        Assert.Equal(["1", "2"], viewer.FindAll("sup a").Select(link => link.TextContent));
        Assert.False(string.IsNullOrEmpty(Assert.Single(ScrolledIds())));
    }
}
