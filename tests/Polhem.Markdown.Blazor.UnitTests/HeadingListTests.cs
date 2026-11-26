using Bunit;
using Microsoft.AspNetCore.Components;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>The headings a host lists in a table of contents, and scrolls to (polhem-dev/polhem-markdown#71).</summary>
public class HeadingListTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown.js";

    private readonly BunitJSModuleInterop _module;

    public HeadingListTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        _module.SetupVoid("scrollToAnchor", _ => true).SetVoidResult();
    }

    [Fact(DisplayName = "The viewer lists the text's headings, with the ids its headings carry")]
    public void Headings_ListsHeadingsWithRenderedIds()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters.Add(component => component.Markdown, "# Guide\n\ntext\n\n## Install\n\n## Install"));

        Assert.Equal(["guide", "install", "install-1"], viewer.Instance.Headings.Select(heading => heading.Id));
        Assert.Equal(["Guide", "Install", "Install"], viewer.FindAll("h1, h2").Select(heading => heading.TextContent));
    }

    [Fact(DisplayName = "A change of headings is reported once, and text that leaves the headings as they were is not")]
    public void OnHeadingsChanged_RaisedOnlyWhenHeadingsChange()
    {
        var reported = new List<IReadOnlyList<MarkdownHeading>>();
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(component => component.Markdown, "# Title\n\nA reply that")
            .Add(component => component.OnHeadingsChanged, headings => reported.Add(headings)));
        Assert.Single(reported);

        viewer.Render(parameters => parameters.Add(component => component.Markdown, "# Title\n\nA reply that grows"));
        Assert.Single(reported);

        viewer.Render(parameters => parameters.Add(component => component.Markdown, "# Title\n\nA reply that grows\n\n## Next"));
        Assert.Equal(2, reported.Count);
        Assert.Equal(["title", "next"], reported[^1].Select(heading => heading.Id));
    }

    [Fact(DisplayName = "Scrolling to a heading scrolls its element to the top and focuses it")]
    public async Task ScrollToHeadingAsync_ScrollsHeadingElement()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters.Add(component => component.Markdown, "# Guide\n\n## Install"));

        Assert.True(await viewer.InvokeAsync(() => viewer.Instance.ScrollToHeadingAsync("install")));

        var element = Assert.IsType<ElementReference>(Assert.Single(_module.Invocations["scrollToAnchor"]).Arguments[0]);
        Assert.Equal(viewer.Find("h2").GetAttribute("blazor:elementReference"), element.Id);
    }

    [Fact(DisplayName = "Scrolling to an id no heading has, such as a footnote's, does nothing and returns false")]
    public async Task ScrollToHeadingAsync_NotAHeading_ReturnsFalse()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters.Add(component => component.Markdown, "## Setup\n\nText[^1].\n\n[^1]: A note."));

        Assert.False(await viewer.InvokeAsync(() => viewer.Instance.ScrollToHeadingAsync("nowhere")));
        Assert.False(await viewer.InvokeAsync(() => viewer.Instance.ScrollToHeadingAsync("fn:1")));
        Assert.Empty(_module.Invocations["scrollToAnchor"]);
    }
}
