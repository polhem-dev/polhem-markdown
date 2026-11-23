namespace Polhem.Markdown.Model;

/// <summary>
/// A reference to a footnote, <c>[^label]</c>, shown as the note's <see cref="Number"/>. <see cref="Id"/> is the anchor a
/// back link returns to; <see cref="NoteId"/> is the <see cref="FootnoteNode.Id"/> of the note it moves to.
/// </summary>
internal sealed record FootnoteReferenceNode(SourceRange Range, int Number, string Id, string NoteId) : InlineNode(Range);
