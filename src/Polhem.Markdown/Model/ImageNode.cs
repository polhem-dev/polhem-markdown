namespace Polhem.Markdown.Model;

/// <summary>
/// An image whose source passed the safety policy: an absolute URI, written so or resolved against
/// <see cref="MarkdownOptions.BaseUri"/>, with a scheme in <see cref="MarkdownOptions.AllowedImageSchemes"/>. A viewer
/// shows <see cref="AlternativeText"/> while the image loads and when it cannot be loaded.
/// </summary>
internal sealed record ImageNode(SourceRange Range, Uri Source, string AlternativeText, string? Title) : InlineNode(Range);
