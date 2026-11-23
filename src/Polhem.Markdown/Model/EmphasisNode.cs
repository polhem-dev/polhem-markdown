namespace Polhem.Markdown.Model;

/// <summary>Italic, bold or struck-through text, by <see cref="Kind"/>; bold italic is one nested in the other.</summary>
internal sealed record EmphasisNode(SourceRange Range, EmphasisKind Kind, IReadOnlyList<InlineNode> Inlines) : InlineNode(Range);
