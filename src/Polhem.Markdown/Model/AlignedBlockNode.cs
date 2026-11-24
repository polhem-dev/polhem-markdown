namespace Polhem.Markdown.Model;

/// <summary>
/// Blocks centered or right-aligned by an HTML <c>&lt;div align="…"&gt;</c> or <c>&lt;p align="…"&gt;</c>, as README files
/// center their logos and badges. Text lines and images inside are aligned; a block of its own width, such as a table,
/// keeps the viewer's layout.
/// </summary>
internal sealed record AlignedBlockNode(SourceRange Range, BlockAlignment Alignment, IReadOnlyList<BlockNode> Blocks) : BlockNode(Range);
