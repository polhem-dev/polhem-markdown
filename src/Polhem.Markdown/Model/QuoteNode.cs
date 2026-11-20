namespace Polhem.Markdown.Model;

internal sealed record QuoteNode(SourceRange Range, IReadOnlyList<BlockNode> Blocks) : BlockNode(Range);
