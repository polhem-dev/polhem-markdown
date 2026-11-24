using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// An image in a run of text: its alternative text until the image has loaded, and for good when it cannot be loaded.
/// The image is shown at its own size and scaled down, never up, to fit the viewer's width.
/// </summary>
internal sealed class ImageView : Decorator
{
    private readonly MarkdownViewer _viewer;
    private readonly Task<LoadedImage?> _loading;
    private Task? _showing;

    public ImageView(MarkdownViewer viewer, ImageNode image)
    {
        _viewer = viewer;
        Source = image.Source;
        Child = new TextBlock { Text = image.AlternativeText };
        if (image.Title is { } title)
        {
            ToolTip.SetTip(this, title);
        }
        _loading = viewer.LoadImage(image.Source);
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
            Child = new Image
            {
                Source = loaded.Image,
                Stretch = Stretch.Uniform,
                StretchDirection = StretchDirection.DownOnly,
                MaxWidth = loaded.Size.Width,
                MaxHeight = loaded.Size.Height,
            };
        }
    }
}
