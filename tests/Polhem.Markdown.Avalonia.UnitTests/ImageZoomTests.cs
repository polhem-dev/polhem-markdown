using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Polhem.Markdown.Avalonia.Images;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>An image opened at full size over the window, to zoom and pan (polhem-dev/polhem-markdown#72).</summary>
[Collection(HeadlessCollection.Name)]
public class ImageZoomTests(HeadlessSession session)
{
    private const string Picture = "Before ![a chart](https://example.org/chart.png) after.";

    /// <summary>Shows a viewer whose images are a PNG of the given size, and waits for them to load.</summary>
    private static async Task<(Window Window, MarkdownViewer Viewer, ImageView Image)> ShowLoaded(
        string markdown, bool zoom = true, int width = 1200, int height = 900)
    {
        var viewer = new MarkdownViewer
        {
            ImageLoader = TestImages.Serve(TestImages.Png(width, height)),
            ZoomImages = zoom,
            Markdown = markdown,
        };
        var window = new Window { Width = 600, Height = 800, Content = viewer };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var image = Descendants<ImageView>(viewer).First();
        await image.Shown.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
        Dispatcher.UIThread.RunJobs();
        return (window, viewer, image);
    }

    private static void Click(Window window, Control control, Point? at = null, int clicks = 1)
    {
        var point = control.TranslatePoint(at ?? new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        for (var click = 0; click < clicks; click++)
        {
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [Fact(DisplayName = "With zooming off, a click on an image opens nothing and the image is no tab stop")]
    public Task ZoomOff_ClickOpensNothing() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture, zoom: false);

        Click(window, image);

        Assert.Null(viewer.ImageZoom);
        Assert.False(image.Focusable);
    });

    [Fact(DisplayName = "A click on an image opens it over the window, fitted to it, and Escape closes it and gives the image back the focus")]
    public Task Click_OpensFittedOverlay_EscapeCloses() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture);

        Click(window, image);

        var overlay = Assert.IsType<ImageZoomOverlay>(viewer.ImageZoom);
        Assert.Contains(overlay, OverlayLayer.GetOverlayLayer(window)!.Children);
        Assert.Equal(window.ClientSize, overlay.Bounds.Size);
        Assert.Equal(overlay.FitScale, overlay.Scale, 3);
        Assert.True(overlay.Scale < 1);
        Assert.Equal(overlay.Bounds.Width / 2, overlay.ImageBounds.Center.X, 1);
        Assert.True(overlay.IsFocused);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.Null(viewer.ImageZoom);
        Assert.DoesNotContain(overlay, OverlayLayer.GetOverlayLayer(window)!.Children);
        Assert.True(image.IsFocused);
    });

    [Fact(DisplayName = "A handler that handles the event shows the image itself, and the viewer opens nothing")]
    public Task Handler_HandlesActivation() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture);
        var activated = new List<(Uri, string)>();
        viewer.ImageActivated += (_, e) =>
        {
            activated.Add((e.Uri, e.AlternativeText));
            e.Handled = true;
        };

        Click(window, image);

        Assert.Equal([(new Uri("https://example.org/chart.png"), "a chart")], activated);
        Assert.Null(viewer.ImageZoom);
    });

    [Theory(DisplayName = "A click on an image inside a link opens the link, not the image, whether zooming is on or off")]
    [InlineData(true)]
    [InlineData(false)]
    public Task ImageInLink_OpensLink(bool zoom) => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded("[![a badge](https://example.org/badge.png)](https://example.org/ci)", zoom);
        var opened = new List<Uri>();
        viewer.LinkActivated += (_, e) =>
        {
            opened.Add(e.Uri);
            e.Handled = true;
        };

        Click(window, image);

        Assert.Null(viewer.ImageZoom);
        Assert.Equal([new Uri("https://example.org/ci")], opened);
        Assert.False(image.Focusable);
    });

    [Fact(DisplayName = "Tab reaches an image that opens, and Enter opens it")]
    public Task Keyboard_TabAndEnterOpen() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.True(image.IsFocused);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.NotNull(viewer.ImageZoom);
    });

    [Fact(DisplayName = "An image that opens can be invoked by an assistive technology")]
    public Task Automation_ImageCanBeInvoked() => session.RunAsync(async () =>
    {
        var (_, viewer, image) = await ShowLoaded(Picture);

        var peer = ControlAutomationPeer.CreatePeerForElement(image);
        Assert.Equal(AutomationControlType.Image, peer.GetAutomationControlType());
        peer.GetProvider<IInvokeProvider>()!.Invoke();

        Assert.NotNull(viewer.ImageZoom);
    });

    [Fact(DisplayName = "An image that does not open cannot be invoked")]
    public Task Automation_ImageThatDoesNotOpen_NotInvokable() => session.RunAsync(async () =>
    {
        var (_, _, image) = await ShowLoaded(Picture, zoom: false);

        Assert.Null(ControlAutomationPeer.CreatePeerForElement(image).GetProvider<IInvokeProvider>());
    });

    [Fact(DisplayName = "The wheel zooms around the pointer where the image overflows the window, up to eight times its own size")]
    public Task Wheel_ZoomsAroundPointer() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture);
        Click(window, image);
        var overlay = viewer.ImageZoom!;
        var anchor = overlay.ImageBounds.Center + new Point(40, 0);
        var before = (anchor - overlay.ImageBounds.TopLeft) / overlay.Scale;

        window.MouseWheel(anchor, new Vector(0, 2));
        Dispatcher.UIThread.RunJobs();

        Assert.True(overlay.Scale > overlay.FitScale);
        var after = (anchor - overlay.ImageBounds.TopLeft) / overlay.Scale;
        Assert.Equal(before.X, after.X, 1);
        Assert.Equal(before.Y, after.Y, 1);

        for (var turn = 0; turn < 40; turn++)
        {
            window.MouseWheel(anchor, new Vector(0, 3));
        }
        Assert.Equal(ImageZoomOverlay.MaxScale, overlay.Scale);
    });

    [Fact(DisplayName = "A double click shows the image at its own size, and another fits it again")]
    public Task DoubleClick_TogglesFullSize() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture);
        Click(window, image);
        var overlay = viewer.ImageZoom!;
        var center = overlay.ImageBounds.Center;

        DoubleClick(window, center);
        Assert.Equal(1, overlay.Scale);

        DoubleClick(window, center);
        Assert.Equal(overlay.FitScale, overlay.Scale, 3);
    });

    private static void DoubleClick(Window window, Point point)
    {
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact(DisplayName = "Dragging pans a zoomed image and keeps it open; a click beside the image closes it")]
    public Task Drag_Pans_ClickBesideCloses() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture, width: 400, height: 300);
        Click(window, image);
        var overlay = viewer.ImageZoom!;
        Assert.Equal(1, overlay.Scale);
        var center = overlay.ImageBounds.Center;
        window.MouseWheel(center, new Vector(0, 9));
        Dispatcher.UIThread.RunJobs();
        var before = overlay.ImageBounds.TopLeft;

        window.MouseDown(center, MouseButton.Left);
        window.MouseMove(center + new Point(-60, -40));
        window.MouseUp(center + new Point(-60, -40), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(overlay, viewer.ImageZoom);
        Assert.Equal(before + new Point(-60, -40), overlay.ImageBounds.TopLeft);

        window.KeyPressQwerty(PhysicalKey.Digit0, RawInputModifiers.None);
        Assert.Equal(1, overlay.Scale);
        var beside = new Point(4, overlay.Bounds.Height - 4);
        Assert.False(overlay.ImageBounds.Contains(beside));
        window.MouseDown(beside, MouseButton.Left);
        window.MouseUp(beside, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(viewer.ImageZoom);
    });

    [Fact(DisplayName = "The close button is named, and closes the image")]
    public Task CloseButton_Closes() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture);
        Click(window, image);
        var overlay = viewer.ImageZoom!;

        var close = overlay.Children.OfType<Button>().Single();
        Assert.Equal(ImageZoomOverlay.CloseLabel, ControlAutomationPeer.CreatePeerForElement(close).GetName());
        Click(window, close);

        Assert.Null(viewer.ImageZoom);
    });

    [Fact(DisplayName = "A drag that starts on an image opens nothing")]
    public Task DragFromImage_OpensNothing() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture, width: 40, height: 20);
        var text = Descendants<MarkdownTextBlock>(viewer).Single();

        var start = image.TranslatePoint(new Point(image.Bounds.Width / 2, image.Bounds.Height / 2), window)!.Value;
        var end = text.TranslatePoint(new Point(text.Bounds.Width - 2, text.Bounds.Height / 2), window)!.Value;
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(end);
        window.MouseUp(end, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(viewer.ImageZoom);
    });
}
