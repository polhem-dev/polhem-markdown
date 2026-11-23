namespace Polhem.Markdown.Model;

/// <summary>
/// The footnotes of a text, in number order. It is always the last block, wherever the definitions were written, and
/// holds only the notes that are referenced.
/// </summary>
internal sealed record FootnotesNode(SourceRange Range, IReadOnlyList<FootnoteNode> Notes) : BlockNode(Range);
