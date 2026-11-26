using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>
/// The table of contents the viewer offers: floating from a button, or pinned beside the text
/// (polhem-dev/polhem-markdown#79).
/// </summary>
public class OutlineTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown.js";
    private const string Document = "# Guide\n\ntext\n\n## Install\n\ntext\n\n### Options\n\ntext";

    private readonly BunitJSModuleInterop _module;

    public OutlineTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        _module.SetupVoid("scrollToAnchor", _ => true).SetVoidResult();
        _module.SetupVoid("watchSource", _ => true).SetVoidResult();
        JSInterop.SetupVoid(invocation => invocation.Identifier.EndsWith("focus", StringComparison.Ordinal)).SetVoidResult();
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<MarkdownViewer> Show(string markdown, bool pinned = false, Action<bool>? pinnedChanged = null,
        OutlineSide side = OutlineSide.Left, string? cssClass = null) =>
        Render<MarkdownViewer>(parameters => parameters
            .Add(viewer => viewer.Markdown, markdown)
            .Add(viewer => viewer.ShowOutline, true)
            .Add(viewer => viewer.OutlinePinned, pinned)
            .Add(viewer => viewer.OutlinePinnedChanged, pinnedChanged ?? (_ => { }))
            .Add(viewer => viewer.OutlineSide, side)
            .Add(viewer => viewer.Class, cssClass));

    [Fact(DisplayName = "Off by default: the viewer writes its text as before, with no table of contents")]
    public void Off_ByDefault()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters.Add(component => component.Markdown, "# Guide"));

        Assert.Empty(viewer.FindAll("nav, .pmd-outlined"));
        Assert.Equal("pmd", viewer.Find("div").GetAttribute("class"));
    }

    [Fact(DisplayName = "On, a navigation landmark beside the text holds a button that opens the list; a text without headings has none")]
    public void On_ButtonInLandmark()
    {
        var viewer = Show(Document);

        Assert.Equal("pmd-outlined pmd-outline-left", viewer.Find("div.pmd-outlined").GetAttribute("class"));
        var nav = viewer.Find("nav.pmd-outline");
        Assert.Equal("Contents", nav.GetAttribute("aria-label"));
        var toggle = viewer.Find("button.pmd-outline-toggle");
        Assert.Equal("false", toggle.GetAttribute("aria-expanded"));
        Assert.Empty(viewer.FindAll(".pmd-outline-entry"));
        Assert.NotNull(viewer.Find("div.pmd-outlined > div.pmd > h1"));

        viewer.Render(parameters => parameters.Add(component => component.Markdown, "Just text."));
        Assert.Empty(viewer.FindAll("nav"));
    }

    [Fact(DisplayName = "The button opens the list of headings, indented by level and linking to each, and Escape closes it")]
    public void Button_OpensList_EscapeCloses()
    {
        var viewer = Show(Document);

        viewer.Find("button.pmd-outline-toggle").Click();

        Assert.Equal("true", viewer.Find("button.pmd-outline-toggle").GetAttribute("aria-expanded"));
        var entries = viewer.FindAll("a.pmd-outline-entry");
        Assert.Equal(["Guide", "Install", "Options"], entries.Select(entry => entry.TextContent));
        Assert.Equal(["#guide", "#install", "#options"], entries.Select(entry => entry.GetAttribute("href")));
        Assert.Contains("pmd-outline-level-3", entries[2].GetAttribute("class"), StringComparison.Ordinal);

        viewer.Find(".pmd-outline-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });

        Assert.Empty(viewer.FindAll(".pmd-outline-entry"));
    }

    [Fact(DisplayName = "A click beside the floating list closes it")]
    public void ClickBeside_Closes()
    {
        var viewer = Show(Document);
        viewer.Find("button.pmd-outline-toggle").Click();

        viewer.Find(".pmd-outline-backdrop").Click();

        Assert.Empty(viewer.FindAll(".pmd-outline-entry"));
    }

    [Fact(DisplayName = "Picking a heading scrolls to it, closes the floating list and marks it as the section being read")]
    public void Pick_ScrollsAndMarks()
    {
        var viewer = Show(Document);
        viewer.Find("button.pmd-outline-toggle").Click();

        viewer.FindAll("a.pmd-outline-entry")[1].Click();

        var scrolled = Assert.Single(_module.Invocations["scrollToAnchor"]);
        Assert.IsType<ElementReference>(scrolled.Arguments[0]);
        Assert.Empty(viewer.FindAll(".pmd-outline-entry"));
        Assert.Equal("install", viewer.Instance.CurrentHeading?.Id);
    }

    [Fact(DisplayName = "Pinned, the list shows without a button, and the pin button unpins it through the binding")]
    public void Pinned_ListAndUnpin()
    {
        var changes = new List<bool>();
        var viewer = Show(Document, pinned: true, pinnedChanged: changes.Add);

        Assert.Contains("pmd-outline-pinned", viewer.Find("div.pmd-outlined").GetAttribute("class"), StringComparison.Ordinal);
        Assert.Empty(viewer.FindAll("button.pmd-outline-toggle"));
        Assert.Equal(3, viewer.FindAll("a.pmd-outline-entry").Count);
        var pin = viewer.Find("button.pmd-outline-pin");
        Assert.Equal("Unpin", pin.GetAttribute("aria-label"));
        Assert.Equal("true", pin.GetAttribute("aria-pressed"));

        pin.Click();

        Assert.Equal([false], changes);
    }

    [Fact(DisplayName = "The script reports the section being read, which the list marks as the current location")]
    public void Reading_MarksCurrent()
    {
        var viewer = Show(Document, pinned: true);
        var watch = Assert.Single(_module.Invocations["watchSource"]);
        Assert.Equal(true, watch.Arguments[5]);

        var offset = viewer.Instance.Headings[2].SourceOffset + 2;
        viewer.InvokeAsync(() => new MarkdownViewerBridge(viewer.Instance).ReadingChanged(offset));

        var current = viewer.Find("a.pmd-outline-current");
        Assert.Equal("Options", current.TextContent);
        Assert.Equal("location", current.GetAttribute("aria-current"));
    }

    [Fact(DisplayName = "On the right, and with the host's classes on the outer element, so its variables reach the list")]
    public void RightSide_HostClasses()
    {
        var viewer = Show(Document, side: OutlineSide.Right, cssClass: "themed");

        Assert.Equal("pmd-outlined pmd-outline-right themed", viewer.Find("div.pmd-outlined").GetAttribute("class"));
        Assert.Equal("pmd themed", viewer.Find("div.pmd-outlined > div.pmd").GetAttribute("class"));
    }

    [Fact(DisplayName = "The labels can be translated")]
    public void Labels_Translated()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(component => component.Markdown, Document)
            .Add(component => component.ShowOutline, true)
            .Add(component => component.OutlineLabel, "目錄")
            .Add(component => component.PinOutlineLabel, "釘選"));

        Assert.Equal("目錄", viewer.Find("nav").GetAttribute("aria-label"));
        viewer.Find("button.pmd-outline-toggle").Click();
        Assert.Equal("釘選", viewer.Find("button.pmd-outline-pin").GetAttribute("aria-label"));
        Assert.Equal("目錄", viewer.Find(".pmd-outline-header span").TextContent);
    }
}
