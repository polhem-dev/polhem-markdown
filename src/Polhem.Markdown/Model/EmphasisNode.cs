namespace Polhem.Markdown.Model;

/// <summary>Italic text, or bold when <see cref="IsStrong"/>; bold italic is one nested in the other.</summary>
internal sealed record EmphasisNode(SourceRange Range, bool IsStrong, IReadOnlyList<InlineNode> Inlines) : InlineNode(Range);
