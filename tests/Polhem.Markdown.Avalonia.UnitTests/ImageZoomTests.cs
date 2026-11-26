using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
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

        var close = OverlayButton(overlay, ImageZoomOverlay.CloseLabel);
        Assert.Equal(ImageZoomOverlay.CloseLabel, ControlAutomationPeer.CreatePeerForElement(close).GetName());
        Click(window, close);

        Assert.Null(viewer.ImageZoom);
    });

    [Fact(DisplayName = "The toolbar's buttons light up under the pointer, and a press between them keeps the image open")]
    public Task Toolbar_HoverAndPressBetween() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture);
        Click(window, image);
        var overlay = viewer.ImageZoom!;
        var save = OverlayButton(overlay, ImageZoomOverlay.SaveLabel);
        var close = OverlayButton(overlay, ImageZoomOverlay.CloseLabel);
        var frame = save.GetVisualDescendants().OfType<Border>().First();

        window.MouseMove(save.TranslatePoint(new Point(save.Bounds.Width / 2, save.Bounds.Height / 2), window)!.Value);
        Dispatcher.UIThread.RunJobs();
        Assert.NotEqual(global::Avalonia.Media.Brushes.Transparent, frame.Background);

        var between = save.TranslatePoint(new Point(save.Bounds.Width + 1, save.Bounds.Height / 2), window)!.Value;
        Assert.True(between.X < close.TranslatePoint(default, window)!.Value.X);
        window.MouseDown(between, MouseButton.Left);
        window.MouseUp(between, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.Same(overlay, viewer.ImageZoom);
        Assert.Equal(global::Avalonia.Media.Brushes.Transparent, frame.Background);
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

    private static Button OverlayButton(ImageZoomOverlay overlay, string label) =>
        overlay.GetVisualDescendants().OfType<Button>()
            .Single(button => global::Avalonia.Automation.AutomationProperties.GetName(button) == label);

    [Fact(DisplayName = "The save button saves the image's original bytes under the name its address gives")]
    public Task Save_WritesOriginalBytes() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture, width: 40, height: 30);
        var saved = new MemoryStream();
        string? suggested = null;
        viewer.SaveTarget = name =>
        {
            suggested = name;
            return Task.FromResult<Stream?>(new KeepOpenStream(saved));
        };
        Click(window, image);

        await viewer.SaveImageAsync(new Uri("https://example.org/chart.png"), "a chart", image);

        Assert.Equal("chart.png", suggested);
        Assert.Equal(TestImages.Png(40, 30), saved.ToArray());
    });

    [Fact(DisplayName = "Clicking the save button raises the event, and a handler that handles it saves the image itself")]
    public Task Save_Handler_HandlesIt() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture);
        var requested = new List<(Uri, string)>();
        var opened = 0;
        viewer.ImageSaveRequested += (_, e) =>
        {
            requested.Add((e.Uri, e.AlternativeText));
            e.Handled = true;
        };
        viewer.SaveTarget = _ =>
        {
            opened++;
            return Task.FromResult<Stream?>(null);
        };
        Click(window, image);

        var save = OverlayButton(viewer.ImageZoom!, ImageZoomOverlay.SaveLabel);
        Click(window, save);

        Assert.Equal([(new Uri("https://example.org/chart.png"), "a chart")], requested);
        Assert.Equal(0, opened);
    });

    [Fact(DisplayName = "An image that cannot be fetched again, or a save the user cancels, writes nothing")]
    public Task Save_NothingToWrite() => session.RunAsync(async () =>
    {
        var (window, viewer, image) = await ShowLoaded(Picture);
        var opened = 0;
        viewer.SaveTarget = _ =>
        {
            opened++;
            return Task.FromResult<Stream?>(null);
        };
        Click(window, image);

        await viewer.SaveImageAsync(new Uri("https://example.org/chart.png"), "a chart", image);
        Assert.Equal(1, opened);

        viewer.ImageLoader = (_, _) => Task.FromResult<Stream?>(null);
        await viewer.SaveImageAsync(new Uri("https://example.org/chart.png"), "a chart", viewer);
        Assert.Equal(1, opened);
    });

    /// <summary>A stream that the save's disposal leaves open, so the test can read what was written.</summary>
    private sealed class KeepOpenStream(MemoryStream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => inner.Position = value; }
        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => inner.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }
}
