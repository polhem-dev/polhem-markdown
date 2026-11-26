using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// An image in a run of text: its alternative text until the image has loaded, and for good when it cannot be loaded.
/// The image is shown at the size the text asks for, or else its own, and scaled down to fit the viewer's width. Of a
/// picture with a source for each theme, the one for the viewer's theme is shown. With
/// <see cref="MarkdownViewer.ZoomImages"/> on, a loaded image outside a link opens at full size on a click or Enter.
/// </summary>
internal sealed class ImageView : Decorator
{
    private readonly MarkdownViewer _viewer;
    private readonly int? _width;
    private readonly int? _height;
    private readonly Task<LoadedImage?> _loading;
    private readonly bool _opens;
    private Task? _showing;
    private Point? _pressedAt;

    /// <param name="viewer">The viewer the image is shown in.</param>
    /// <param name="image">The image.</param>
    /// <param name="opens">Whether the image opens at full size when activated: zooming is on, and it is in no link.</param>
    public ImageView(MarkdownViewer viewer, ImageNode image, bool opens)
    {
        _viewer = viewer;
        _opens = opens;
        AlternativeText = image.AlternativeText;
        _width = image.Width;
        _height = image.Height;
        Source = image.SourceFor(viewer.IsDarkTheme);
        Child = new TextBlock { Text = image.AlternativeText };
        AutomationProperties.SetName(this, image.AlternativeText);
        if (image.Title is { } title)
        {
            ToolTip.SetTip(this, title);
        }
        _loading = viewer.LoadImage(Source);
    }

    /// <summary>
    /// An image is one element named by its alternative text, whether it shows the picture or the text; an image with no
    /// alternative text is decorative and left out, as in a browser.
    /// </summary>
    protected override AutomationPeer OnCreateAutomationPeer() =>
        new Automation.LeafAutomationPeer(this, AutomationControlType.Image, _opens ? Activate : null);

    public string AlternativeText { get; }

    /// <summary>The position the image takes in its text, which a link around it covers.</summary>
    internal int TextOffset { get; init; }

    /// <summary>The image once it has loaded, which is what opens at full size.</summary>
    internal LoadedImage? Decoded { get; private set; }

    /// <summary>Whether the image opens at full size when activated, once it has loaded.</summary>
    internal bool Opens => _opens && Decoded is not null;

    private void Activate()
    {
        if (Opens)
        {
            _viewer.ActivateImage(this);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        _pressedAt = Opens && e.ClickCount == 1 && point.Properties.IsLeftButtonPressed ? point.Position : null;
    }

    /// <summary>
    /// Opens the image if the release ends a click on it, which its text passes on, and returns whether it did. A press
    /// that moves on selects text, as a drag that starts on an image does in a browser.
    /// </summary>
    internal bool TakeClick(PointerReleasedEventArgs e)
    {
        if (_pressedAt is not { } pressed)
        {
            return false;
        }
        _pressedAt = null;
        var delta = e.GetPosition(this) - pressed;
        if (Math.Abs(delta.X) + Math.Abs(delta.Y) >= ClickSlop)
        {
            return false;
        }
        Activate();
        return true;
    }

    private const double ClickSlop = 4;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (Opens && (e.Key is Key.Enter or Key.Space) && e.KeyModifiers == KeyModifiers.None)
        {
            Activate();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    /// <summary>The address the image is loaded from.</summary>
    public Uri Source { get; }

    /// <summary>The image once it has loaded, for tests.</summary>
    internal Image? LoadedImage => Child as Image;

    /// <summary>Completes once the view shows the image, or has given up on it, for tests.</summary>
    internal Task Shown => _showing ?? Task.CompletedTask;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewer.PropertyChanged += OnViewerPropertyChanged;
        _showing ??= ShowWhenLoadedAsync();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _viewer.PropertyChanged -= OnViewerPropertyChanged;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        // A control in a run of text is offered unlimited width, so the viewer's own width is the limit.
        var width = _viewer.Bounds.Width;
        return base.MeasureOverride(width > 0 ? availableSize.WithWidth(Math.Min(availableSize.Width, width)) : availableSize);
    }

    private void OnViewerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == BoundsProperty)
        {
            InvalidateMeasure();
        }
    }

    private async Task ShowWhenLoadedAsync()
    {
        // NOTE: a load that fails leaves the alternative text; the exception is observed here so it does not surface as
        // an unobserved task exception, and nothing else needs it.
        await ((Task)_loading).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        if (_loading.IsFaulted)
        {
            _ = _loading.Exception;
            return;
        }
        if (_loading.IsCompletedSuccessfully && _loading.Result is { } loaded)
        {
            Decoded = loaded;
            if (_opens)
            {
                Focusable = true;
                IsTabStop = true;
                Cursor = new Cursor(StandardCursorType.Hand);
            }

            // A size the text asks for scales the image to it, up or down, keeping its proportions; without one the
            // image is never scaled up.
            var sized = _width is not null || _height is not null;
            Child = new Image
            {
                Source = loaded.Image,
                Stretch = Stretch.Uniform,
                StretchDirection = sized ? StretchDirection.Both : StretchDirection.DownOnly,
                MaxWidth = _width ?? (sized ? double.PositiveInfinity : loaded.Size.Width),
                MaxHeight = _height ?? (sized ? double.PositiveInfinity : loaded.Size.Height),
            };
        }
    }
}
