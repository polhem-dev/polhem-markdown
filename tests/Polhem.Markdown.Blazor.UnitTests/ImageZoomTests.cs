using Bunit;
using Microsoft.AspNetCore.Components;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>An image opened at full size over the page, to zoom and pan (polhem-dev/polhem-markdown#72).</summary>
public class ImageZoomTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown.js";
    private const string Picture = "Before ![a chart](https://example.org/chart.png) after.";

    private readonly BunitJSModuleInterop _module;

    public ImageZoomTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        _module.SetupVoid("openZoom", _ => true).SetVoidResult();
    }

    private IRenderedComponent<MarkdownViewer> Show(string markdown, bool zoom = true) =>
        Render<MarkdownViewer>(parameters => parameters
            .Add(viewer => viewer.Markdown, markdown)
            .Add(viewer => viewer.ZoomImages, zoom));

    [Fact(DisplayName = "With zooming off, an image is written as before, with no button")]
    public void ZoomOff_NoButton()
    {
        var viewer = Show(Picture, zoom: false);

        Assert.Empty(viewer.FindAll("button"));
        viewer.MarkupMatches(
            "<div class=\"pmd\"><p>Before <img class=\"pmd-img\" src=\"https://example.org/chart.png\" alt=\"a chart\" "
            + "loading=\"lazy\" decoding=\"async\" referrerpolicy=\"no-referrer\"> after.</p></div>");
    }

    [Fact(DisplayName = "With zooming on, an image is inside a button named by its alternative text")]
    public void ZoomOn_ImageInButton()
    {
        var viewer = Show(Picture);

        var button = viewer.Find("button.pmd-image-button");
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Equal("a chart", button.QuerySelector("img")!.GetAttribute("alt"));
    }

    [Fact(DisplayName = "A click opens the image in a dialog over the page, which the script opens, and its closing takes it away")]
    public void Click_OpensDialog_ClosingRemovesIt()
    {
        var viewer = Show(Picture);

        viewer.Find("button.pmd-image-button").Click();

        var dialog = viewer.Find("dialog.pmd-zoom");
        Assert.Equal("a chart", dialog.GetAttribute("aria-label"));
        Assert.Equal("https://example.org/chart.png", dialog.QuerySelector("img.pmd-zoom-image")!.GetAttribute("src"));
        Assert.Equal("Close", dialog.QuerySelector(".pmd-zoom-toolbar button.pmd-zoom-close")!.GetAttribute("aria-label"));
        var open = Assert.Single(_module.Invocations["openZoom"]);
        Assert.IsType<ElementReference>(open.Arguments[0]);

        viewer.InvokeAsync(() => new MarkdownViewerBridge(viewer.Instance).ZoomClosed());

        Assert.Empty(viewer.FindAll("dialog"));
        Assert.Null(viewer.Instance.ZoomedImage);
    }

    [Fact(DisplayName = "The dialog carries the viewer's classes, so the variables a host sets on them reach it")]
    public void Dialog_CarriesViewerClasses()
    {
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(component => component.Markdown, Picture)
            .Add(component => component.ZoomImages, true)
            .Add(component => component.Class, "themed"));

        viewer.Find("button.pmd-image-button").Click();

        Assert.Equal("pmd pmd-zoom themed", viewer.Find("dialog").GetAttribute("class"));
    }

    [Fact(DisplayName = "A picture with a source for each theme opens with the same sources, so the browser picks the same one")]
    public void Picture_OpensWithThemeSources()
    {
        var viewer = Show("""
            <picture>
              <source media="(prefers-color-scheme: dark)" srcset="https://example.org/dark.png">
              <img alt="Logo" src="https://example.org/light.png">
            </picture>
            """);

        viewer.Find("button.pmd-image-button").Click();

        var source = viewer.Find("dialog.pmd-zoom picture source");
        Assert.Equal("https://example.org/dark.png", source.GetAttribute("srcset"));
    }

    [Fact(DisplayName = "A handler shows the image itself, and the viewer opens nothing")]
    public void Handler_HandlesActivation()
    {
        var activated = new List<MarkdownImageActivatedEventArgs>();
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(component => component.Markdown, Picture)
            .Add(component => component.ZoomImages, true)
            .Add(component => component.OnImageActivated, e => activated.Add(e)));

        viewer.Find("button.pmd-image-button").Click();

        var args = Assert.Single(activated);
        Assert.Equal(new Uri("https://example.org/chart.png"), args.Uri);
        Assert.Equal("a chart", args.AlternativeText);
        Assert.Empty(viewer.FindAll("dialog"));
        Assert.Empty(_module.Invocations["openZoom"]);
    }

    [Fact(DisplayName = "An image inside a link is no button, so it opens the link")]
    public void ImageInLink_NoButton()
    {
        var viewer = Show("[![a badge](https://example.org/badge.png)](https://example.org/ci)");

        Assert.Empty(viewer.FindAll("button"));
        Assert.NotNull(viewer.Find("a img"));
    }

    [Fact(DisplayName = "The save button asks the script to download the image under the name its address gives")]
    public void Save_CallsScript()
    {
        _module.SetupVoid("saveImage", _ => true).SetVoidResult();
        var viewer = Show(Picture);
        viewer.Find("button.pmd-image-button").Click();

        var save = viewer.Find("dialog button.pmd-zoom-save");
        Assert.Equal("Save image", save.GetAttribute("aria-label"));
        save.Click();

        var call = Assert.Single(_module.Invocations["saveImage"]);
        Assert.IsType<ElementReference>(call.Arguments[0]);
        Assert.Equal("chart.png", call.Arguments[1]);
    }

    [Fact(DisplayName = "A host that handles saving gets the image, and the script downloads nothing")]
    public void Save_HostHandles()
    {
        var requested = new List<MarkdownImageSaveRequestedEventArgs>();
        var viewer = Render<MarkdownViewer>(parameters => parameters
            .Add(component => component.Markdown, Picture)
            .Add(component => component.ZoomImages, true)
            .Add(component => component.OnImageSaveRequested, e => requested.Add(e)));
        viewer.Find("button.pmd-image-button").Click();

        viewer.Find("dialog button.pmd-zoom-save").Click();

        var args = Assert.Single(requested);
        Assert.Equal(new Uri("https://example.org/chart.png"), args.Uri);
        Assert.Equal("a chart", args.AlternativeText);
        Assert.Empty(_module.Invocations["saveImage"]);
    }
}
