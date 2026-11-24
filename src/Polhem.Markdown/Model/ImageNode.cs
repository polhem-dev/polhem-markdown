namespace Polhem.Markdown.Model;

/// <summary>
/// An image whose source passed the safety policy: an absolute URI, written so or resolved against
/// <see cref="MarkdownOptions.BaseUri"/>, with a scheme in <see cref="MarkdownOptions.AllowedImageSchemes"/>. A viewer
/// shows <see cref="AlternativeText"/> while the image loads and when it cannot be loaded.
/// </summary>
internal sealed record ImageNode(SourceRange Range, Uri Source, string AlternativeText, string? Title) : InlineNode(Range)
{
    /// <summary>The width in pixels an HTML <c>img</c> asked for, if any.</summary>
    public int? Width { get; init; }

    /// <summary>The height in pixels an HTML <c>img</c> asked for, if any.</summary>
    public int? Height { get; init; }

    /// <summary>
    /// The source to show instead of <see cref="Source"/> with a dark theme, from a <c>picture</c> whose <c>source</c>
    /// asks for <c>(prefers-color-scheme: dark)</c>; it passed the same policy.
    /// </summary>
    public Uri? DarkSource { get; init; }

    /// <summary>The source to show instead of <see cref="Source"/> with a light theme, as <see cref="DarkSource"/> is for a dark one.</summary>
    public Uri? LightSource { get; init; }

    /// <summary>The source to show with a dark or a light theme.</summary>
    public Uri SourceFor(bool dark) => (dark ? DarkSource : LightSource) ?? Source;
}
