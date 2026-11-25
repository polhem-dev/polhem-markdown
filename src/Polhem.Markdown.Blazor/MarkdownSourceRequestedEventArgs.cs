namespace Polhem.Markdown.Blazor;

/// <summary>
/// Data of <see cref="MarkdownViewer.OnSourceRequested"/>: where in the Markdown text a user asked to go.
/// </summary>
public sealed class MarkdownSourceRequestedEventArgs
{
    /// <summary>Initializes a new instance of the <see cref="MarkdownSourceRequestedEventArgs"/> class.</summary>
    /// <param name="sourceOffset">The offset in the Markdown text.</param>
    public MarkdownSourceRequestedEventArgs(int sourceOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(sourceOffset);
        SourceOffset = sourceOffset;
    }

    /// <summary>
    /// Gets the offset in <see cref="MarkdownViewer.Markdown"/>, in UTF-16 code units, of the text where the user clicked.
    /// </summary>
    public int SourceOffset { get; }
}
