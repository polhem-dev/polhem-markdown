namespace Polhem.Markdown.Model;

/// <summary>
/// A table. <see cref="Alignments"/> has one entry per column; a row may have fewer cells than there are columns.
/// </summary>
internal sealed record TableNode(
    SourceRange Range,
    IReadOnlyList<ColumnAlignment> Alignments,
    IReadOnlyList<TableRowNode> HeaderRows,
    IReadOnlyList<TableRowNode> Rows) : BlockNode(Range);
