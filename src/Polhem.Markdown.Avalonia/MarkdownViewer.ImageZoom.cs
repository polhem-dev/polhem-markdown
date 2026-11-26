using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Polhem.Markdown.Avalonia.Images;

namespace Polhem.Markdown.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>Defines the <see cref="ZoomImages"/> property.</summary>
    public static readonly StyledProperty<bool> ZoomImagesProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(ZoomImages));

    /// <summary>Defines the <see cref="ImageActivated"/> event, which bubbles.</summary>
    public static readonly RoutedEvent<MarkdownImageActivatedEventArgs> ImageActivatedEvent =
        RoutedEvent.Register<MarkdownViewer, MarkdownImageActivatedEventArgs>(nameof(ImageActivated), RoutingStrategies.Bubble);

    /// <summary>
    /// Gets or sets whether a click on an image, or Enter on it, opens it at full size over the window, where it can be
    /// zoomed and panned. Off by default. An image inside a link opens the link instead.
    /// </summary>
    public bool ZoomImages
    {
        get => GetValue(ZoomImagesProperty);
        set => SetValue(ZoomImagesProperty, value);
    }

    /// <summary>
    /// Occurs when a user opens an image while <see cref="ZoomImages"/> is on. A handler that marks the event handled
    /// shows the image its own way, and the viewer opens nothing.
    /// </summary>
    public event EventHandler<MarkdownImageActivatedEventArgs>? ImageActivated
    {
        add => AddHandler(ImageActivatedEvent, value);
        remove => RemoveHandler(ImageActivatedEvent, value);
    }

    /// <summary>The image open over the window, for tests.</summary>
    internal ImageZoomOverlay? ImageZoom { get; private set; }

    /// <summary>
    /// Raises <see cref="ImageActivatedEvent"/> from <paramref name="view"/>, and opens the image over the window when no
    /// handler handled it.
    /// </summary>
    internal void ActivateImage(ImageView view)
    {
        if (view.Decoded is not { } loaded)
        {
            return;
        }
        var args = new MarkdownImageActivatedEventArgs(view.Source, view.AlternativeText);
        view.RaiseEvent(args);
        if (args.Handled || ImageZoom is not null || OverlayLayer.GetOverlayLayer(this) is not { } layer)
        {
            return;
        }

        var overlay = new ImageZoomOverlay(loaded, view.AlternativeText);
        overlay.Closed += (_, _) =>
        {
            layer.Children.Remove(overlay);
            ImageZoom = null;
            view.Focus(global::Avalonia.Input.NavigationMethod.Unspecified);
        };
        ImageZoom = overlay;
        layer.Children.Add(overlay);
        overlay.Focus(global::Avalonia.Input.NavigationMethod.Unspecified);
    }
}
