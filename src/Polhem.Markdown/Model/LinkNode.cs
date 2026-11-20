namespace Polhem.Markdown.Model;

/// <summary>A link whose target passed the safety policy: an absolute URI with an allowed scheme.</summary>
internal sealed record LinkNode(SourceRange Range, Uri Target, IReadOnlyList<InlineNode> Inlines) : InlineNode(Range);
