namespace Polhem.Markdown.Model;

/// <summary>
/// Text set apart by <see cref="Kind"/>: italic, bold, struck through, raised, lowered, or a key; bold italic is one
/// nested in the other.
/// </summary>
internal sealed record EmphasisNode(SourceRange Range, EmphasisKind Kind, IReadOnlyList<InlineNode> Inlines) : InlineNode(Range);
