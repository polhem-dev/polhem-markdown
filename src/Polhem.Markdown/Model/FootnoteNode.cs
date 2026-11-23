namespace Polhem.Markdown.Model;

/// <summary>A footnote, numbered by its first reference; its last paragraph ends with the back links.</summary>
internal sealed record FootnoteNode(SourceRange Range, int Number, string Id, IReadOnlyList<BlockNode> Blocks) : BlockNode(Range);
