using Avalonia.Interactivity;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Data of <see cref="MarkdownViewer.ImageSaveRequestedEvent"/>: the image a user asked to save from the zoom overlay.
/// </summary>
public sealed class MarkdownImageSaveRequestedEventArgs : RoutedEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="MarkdownImageSaveRequestedEventArgs"/> class.</summary>
    /// <param name="uri">The address the image was loaded from.</param>
    /// <param name="alternativeText">The image's alternative text.</param>
    public MarkdownImageSaveRequestedEventArgs(Uri uri, string alternativeText)
        : base(MarkdownViewer.ImageSaveRequestedEvent)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(alternativeText);
        Uri = uri;
        AlternativeText = alternativeText;
    }

    /// <summary>Gets the address the image was loaded from.</summary>
    public Uri Uri { get; }

    /// <summary>Gets the image's alternative text, which is empty for a decorative image.</summary>
    public string AlternativeText { get; }
}
