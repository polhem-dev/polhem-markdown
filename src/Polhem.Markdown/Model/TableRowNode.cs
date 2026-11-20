namespace Polhem.Markdown.Model;

internal sealed record TableRowNode(SourceRange Range, IReadOnlyList<TableCellNode> Cells) : BlockNode(Range);
