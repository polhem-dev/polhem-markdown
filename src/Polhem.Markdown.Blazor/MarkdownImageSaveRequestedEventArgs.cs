namespace Polhem.Markdown.Blazor;

/// <summary>Data of <see cref="MarkdownViewer.OnImageSaveRequested"/>: the image a user asked to save from the zoom overlay.</summary>
public sealed class MarkdownImageSaveRequestedEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="MarkdownImageSaveRequestedEventArgs"/> class.</summary>
    /// <param name="uri">The address of the image.</param>
    /// <param name="alternativeText">The image's alternative text.</param>
    public MarkdownImageSaveRequestedEventArgs(Uri uri, string alternativeText)
    {
        ArgumentNullException.ThrowIfNull(uri);
        ArgumentNullException.ThrowIfNull(alternativeText);
        Uri = uri;
        AlternativeText = alternativeText;
    }

    /// <summary>
    /// Gets the address of the image. Of a picture with a source for each theme, it is the address of the image the
    /// text gives for when no theme matches.
    /// </summary>
    public Uri Uri { get; }

    /// <summary>Gets the image's alternative text, which is empty for a decorative image.</summary>
    public string AlternativeText { get; }
}
