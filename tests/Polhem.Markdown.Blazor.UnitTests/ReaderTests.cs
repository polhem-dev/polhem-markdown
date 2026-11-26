using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>
/// The reader around a viewer, with its table of contents: a rail along its edge, a list that slides out over the text,
/// and a pin that keeps it beside the text (polhem-dev/polhem-markdown#79).
/// </summary>
public class ReaderTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown.js";
    private const string Document = "# Guide\n\ntext\n\n## Install\n\ntext\n\n### Options\n\ntext";

    private readonly BunitJSModuleInterop _module;

    public ReaderTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        _module.SetupVoid("scrollToAnchor", _ => true).SetVoidResult();
        _module.SetupVoid("watchSource", _ => true).SetVoidResult();
        JSInterop.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<MarkdownReader> Show(string markdown, bool pinned = false, Action<bool>? pinnedChanged = null,
        OutlineSide side = OutlineSide.Left, string? cssClass = null) =>
        Render<MarkdownReader>(parameters => parameters
            .Add(reader => reader.OutlinePinned, pinned)
            .Add(reader => reader.OutlinePinnedChanged, pinnedChanged ?? (_ => { }))
            .Add(reader => reader.OutlineSide, side)
            .Add(reader => reader.Class, cssClass)
            .AddChildContent<MarkdownViewer>(viewer => viewer.Add(component => component.Markdown, markdown)));

    [Fact(DisplayName = "The reader scrolls the viewer in a centred column, beside a navigation landmark with the rail")]
    public void Reader_Layout()
    {
        var reader = Show(Document);

        Assert.Equal("pmd-reader pmd-reader-left", reader.Find("div.pmd-reader").GetAttribute("class"));
        Assert.Equal("--pmd-reader-text-width: 800px", reader.Find("div.pmd-reader").GetAttribute("style"));
        Assert.NotNull(reader.Find(".pmd-reader-scroll > .pmd-reader-text > div.pmd > h1"));
        Assert.Equal("Contents", reader.Find("nav.pmd-reader-outline").GetAttribute("aria-label"));
        var rail = reader.Find("button.pmd-reader-rail");
        Assert.Equal("false", rail.GetAttribute("aria-expanded"));
        Assert.Equal(
            ["pmd-reader-mark pmd-reader-mark-1", "pmd-reader-mark pmd-reader-mark-2", "pmd-reader-mark pmd-reader-mark-3"],
            rail.QuerySelectorAll("span").Select(mark => mark.GetAttribute("class")));
        Assert.Equal(["Guide", "Install", "Options"], reader.FindAll("a.pmd-reader-entry").Select(entry => entry.TextContent));
    }

    [Fact(DisplayName = "A text without headings has no rail")]
    public void NoHeadings_NoRail()
    {
        var reader = Show("Just text.");

        Assert.Empty(reader.FindAll("nav"));
        Assert.Contains("pmd-reader-plain", reader.Find("div.pmd-reader").GetAttribute("class"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A click on the rail opens the list to stay, and Escape, the rail again, or a click beside it closes it")]
    public void Rail_OpensAndCloses()
    {
        var reader = Show(Document);

        reader.Find("button.pmd-reader-rail").Click();
        Assert.Contains("pmd-reader-open", reader.Find("div.pmd-reader").GetAttribute("class"), StringComparison.Ordinal);
        Assert.Equal("true", reader.Find("button.pmd-reader-rail").GetAttribute("aria-expanded"));

        reader.Find(".pmd-reader-panel").KeyDown(new KeyboardEventArgs { Key = "Escape" });
        Assert.DoesNotContain("pmd-reader-open", reader.Find("div.pmd-reader").GetAttribute("class"), StringComparison.Ordinal);

        reader.Find("button.pmd-reader-rail").Click();
        reader.Find("button.pmd-reader-rail").Click();
        Assert.DoesNotContain("pmd-reader-open", reader.Find("div.pmd-reader").GetAttribute("class"), StringComparison.Ordinal);

        reader.Find("button.pmd-reader-rail").Click();
        reader.Find(".pmd-reader-backdrop").Click();
        Assert.Empty(reader.FindAll(".pmd-reader-backdrop"));
    }

    [Fact(DisplayName = "Picking a heading scrolls to it, closes the list and marks it on the rail and the list")]
    public void Pick_ScrollsAndMarks()
    {
        var reader = Show(Document);
        reader.Find("button.pmd-reader-rail").Click();

        reader.FindAll("a.pmd-reader-entry")[1].Click();

        Assert.IsType<ElementReference>(Assert.Single(_module.Invocations["scrollToAnchor"]).Arguments[0]);
        Assert.DoesNotContain("pmd-reader-open", reader.Find("div.pmd-reader").GetAttribute("class"), StringComparison.Ordinal);
        Assert.Equal("install", reader.Instance.CurrentHeading?.Id);
        Assert.Contains("pmd-reader-current", reader.FindAll("span.pmd-reader-mark")[1].GetAttribute("class"), StringComparison.Ordinal);
        Assert.Equal("location", reader.FindAll("a.pmd-reader-entry")[1].GetAttribute("aria-current"));
    }

    [Fact(DisplayName = "The viewer's script reports the section being read, which the rail and the list mark")]
    public void Reading_MarksCurrent()
    {
        var reader = Show(Document, pinned: true);
        var viewer = reader.FindComponent<MarkdownViewer>();
        Assert.Equal(true, Assert.Single(_module.Invocations["watchSource"]).Arguments[5]);

        var offset = viewer.Instance.Headings[2].SourceOffset + 2;
        reader.InvokeAsync(() => new MarkdownViewerBridge(viewer.Instance).ReadingChanged(offset));

        Assert.Equal("Options", reader.Find("a.pmd-reader-current").TextContent);
        Assert.Contains("pmd-reader-current", reader.FindAll("span.pmd-reader-mark")[2].GetAttribute("class"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A viewer on its own reports no reading position")]
    public void ViewerAlone_NoReading()
    {
        Render<MarkdownViewer>(parameters => parameters.Add(component => component.Markdown, Document));

        Assert.Empty(_module.Invocations["watchSource"]);
    }

    [Fact(DisplayName = "Pinned, the list stands beside the text, and the pin unpins it through the binding")]
    public void Pinned_Unpin()
    {
        var changes = new List<bool>();
        var reader = Show(Document, pinned: true, pinnedChanged: changes.Add);

        Assert.Contains("pmd-reader-pinned", reader.Find("div.pmd-reader").GetAttribute("class"), StringComparison.Ordinal);
        var pin = reader.Find("button.pmd-reader-pin");
        Assert.Equal("Unpin", pin.GetAttribute("aria-label"));
        Assert.Equal("true", pin.GetAttribute("aria-pressed"));

        pin.Click();

        Assert.Equal([false], changes);
    }

    [Fact(DisplayName = "On the right, with the host's classes, labels translated and no limit on the text's width")]
    public void RightSide_Classes_Labels()
    {
        var reader = Render<MarkdownReader>(parameters => parameters
            .Add(component => component.OutlineSide, OutlineSide.Right)
            .Add(component => component.Class, "themed")
            .Add(component => component.OutlineLabel, "目錄")
            .Add(component => component.PinOutlineLabel, "釘選")
            .Add(component => component.TextMaxWidth, (int?)null)
            .AddChildContent<MarkdownViewer>(viewer => viewer.Add(component => component.Markdown, Document)));

        var root = reader.Find("div.pmd-reader");
        Assert.Equal("pmd-reader pmd-reader-right themed", root.GetAttribute("class"));
        Assert.Null(root.GetAttribute("style"));
        Assert.Equal("目錄", reader.Find("nav").GetAttribute("aria-label"));
        Assert.Equal("釘選", reader.Find("button.pmd-reader-pin").GetAttribute("aria-label"));
    }

    [Fact(DisplayName = "The list follows the text as it changes, as a reply that streams in")]
    public void Headings_FollowText()
    {
        var reader = Show("# One\n\ntext");
        var viewer = reader.FindComponent<MarkdownViewer>();

        viewer.Render(parameters => parameters.Add(component => component.Markdown, "# One\n\ntext\n\n## Two"));

        Assert.Equal(["One", "Two"], reader.FindAll("a.pmd-reader-entry").Select(entry => entry.TextContent));
    }
}
