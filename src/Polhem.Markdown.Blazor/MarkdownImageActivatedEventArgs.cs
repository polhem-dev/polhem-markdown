namespace Polhem.Markdown.Blazor;

/// <summary>
/// Data of <see cref="MarkdownViewer.OnImageActivated"/>: the image a user clicked, or pressed Enter on, to see it at
/// full size.
/// </summary>
public sealed class MarkdownImageActivatedEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="MarkdownImageActivatedEventArgs"/> class.</summary>
    /// <param name="uri">The address of the image.</param>
    /// <param name="alternativeText">The image's alternative text.</param>
    public MarkdownImageActivatedEventArgs(Uri uri, string alternativeText)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(alternativeText);
        Uri = uri;
        AlternativeText = alternativeText;
    }

    /// <summary>
    /// Gets the address of the image. Of a picture with a source for each theme, it is the address of the image the
    /// text gives for when no theme matches; the browser picks the one it shows.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>Gets the image's alternative text, which is empty for a decorative image.</summary>
    public string AlternativeText { get; }
}
