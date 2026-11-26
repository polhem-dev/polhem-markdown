using Avalonia.Interactivity;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Data of <see cref="MarkdownViewer.ImageActivatedEvent"/>: the image a user clicked, or pressed Enter on, to see it at
/// full size.
/// </summary>
public sealed class MarkdownImageActivatedEventArgs : RoutedEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="MarkdownImageActivatedEventArgs"/> class.</summary>
    /// <param name="uri">The address the image was loaded from.</param>
    /// <param name="alternativeText">The image's alternative text.</param>
    public MarkdownImageActivatedEventArgs(Uri uri, string alternativeText)
        : base(MarkdownViewer.ImageActivatedEvent)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(alternativeText);
        Uri = uri;
        AlternativeText = alternativeText;
    }

    /// <summary>
    /// Gets the address the image was loaded from: of a picture with a source for each theme, the one for the viewer's
    /// theme.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>Gets the image's alternative text, which is empty for a decorative image.</summary>
    public string AlternativeText { get; }
}
