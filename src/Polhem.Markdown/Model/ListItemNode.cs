namespace Polhem.Markdown.Model;

internal sealed record ListItemNode(SourceRange Range, IReadOnlyList<BlockNode> Blocks) : BlockNode(Range);
