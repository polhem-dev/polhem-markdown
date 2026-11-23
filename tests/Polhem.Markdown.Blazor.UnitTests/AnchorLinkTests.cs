using Bunit;
using Microsoft.AspNetCore.Components;

namespace Polhem.Markdown.Blazor.UnitTests;

public class AnchorLinkTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown.js";

    private readonly BunitJSModuleInterop _module;

    public AnchorLinkTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        _module.SetupVoid("scrollToHeading", _ => true).SetVoidResult();
    }

    private IRenderedComponent<MarkdownViewer> Show(string markdown) =>
        Render<MarkdownViewer>(parameters => parameters.Add(viewer => viewer.Markdown, markdown));

    /// <summary>The ids of the headings the module scrolled to and focused so far, in order.</summary>
    private List<string> ScrolledIds() => _module.Invocations["scrollToHeading"]
        .Select(invocation => Assert.IsType<ElementReference>(invocation.Arguments[0]).Id)
        .ToList();

    [Fact(DisplayName = "A link to a heading points at the heading's id, and the heading can take focus")]
    public void Render_AnchorLink_PointsAtHeading()
    {
        var viewer = Show("[setup](#setup)\n\n## Setup");

        viewer.MarkupMatches(
            "<div class=\"pmd\"><p><a href=\"#setup\">setup</a></p><h2 tabindex=\"-1\">Setup</h2></div>");
    }

    [Fact(DisplayName = "The heading's id is not written as an HTML id, so it cannot clash with the page")]
    public void Render_Heading_HasNoHtmlId()
    {
        var viewer = Show("## Setup");

        Assert.Null(viewer.Find("h2").GetAttribute("id"));
    }

    [Fact(DisplayName = "Clicking a link to a heading scrolls that heading to the top and focuses it, each link its own heading, every time")]
    public void Click_AnchorLinks_FocusTheirHeadings()
    {
        var viewer = Show("[one](#one) [two](#two)\n\n## One\n\n## Two");

        viewer.FindAll("a")[0].Click();
        viewer.FindAll("a")[1].Click();
        viewer.FindAll("a")[0].Click();

        var ids = ScrolledIds();
        Assert.Equal(3, ids.Count);
        Assert.All(ids, id => Assert.False(string.IsNullOrEmpty(id)));
        Assert.NotEqual(ids[0], ids[1]);
        Assert.Equal(ids[0], ids[2]);
    }

    [Fact(DisplayName = "A link to a heading still works after the heading text changed while a reply arrived")]
    public void Click_AnchorLink_AfterHeadingGrew_FocusesHeading()
    {
        var viewer = Show("[usage](#usage)\n\n## Usa");
        Assert.Empty(viewer.FindAll("a"));

        viewer.Render(parameters => parameters.Add(component => component.Markdown, "[usage](#usage)\n\n## Usage"));
        viewer.Find("a").Click();

        Assert.False(string.IsNullOrEmpty(Assert.Single(ScrolledIds())));
    }

    [Fact(DisplayName = "A link to a heading is handled by the viewer and never reported to the host")]
    public void Click_AnchorLink_DoesNotRaiseOnLinkActivated()
    {
        var reported = false;
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(component => component.Markdown, "[setup](#setup)\n\n## Setup")
            .Add(component => component.OnLinkActivated, _ => reported = true));

        viewer.Find("a").Click();

        Assert.False(reported);
    }

    [Fact(DisplayName = "The script module is loaded only when a link to a heading is first clicked, and only once")]
    public void Click_AnchorLink_LoadsModuleOnce()
    {
        var viewer = Show("[one](#one) [two](#two)\n\n## One\n\n## Two");
        Assert.Empty(JSInterop.Invocations["import"]);

        viewer.FindAll("a")[0].Click();
        viewer.FindAll("a")[1].Click();

        Assert.Single(JSInterop.Invocations["import"]);
    }
}
