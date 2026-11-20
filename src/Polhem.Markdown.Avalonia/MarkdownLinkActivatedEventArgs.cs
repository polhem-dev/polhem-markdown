using Avalonia.Interactivity;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Data of <see cref="MarkdownViewer.LinkActivatedEvent"/>: the link a user clicked.
/// </summary>
public sealed class MarkdownLinkActivatedEventArgs : RoutedEventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownLinkActivatedEventArgs"/> class.
    /// </summary>
    /// <param name="uri">The target of the link.</param>
    public MarkdownLinkActivatedEventArgs(Uri uri)
        : base(MarkdownViewer.LinkActivatedEvent)
    {
        ArgumentNullException.ThrowIfNull(uri);
        Uri = uri;
    }

    /// <summary>
    /// Gets the target of the link: an absolute URI whose scheme is one of
    /// <see cref="MarkdownOptions.AllowedLinkSchemes"/>.
    /// </summary>
    public Uri Uri { get; }
}
