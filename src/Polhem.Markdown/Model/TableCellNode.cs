namespace Polhem.Markdown.Model;

internal sealed record TableCellNode(SourceRange Range, IReadOnlyList<InlineNode> Inlines) : BlockNode(Range);
