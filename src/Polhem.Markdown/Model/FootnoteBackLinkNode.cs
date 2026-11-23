namespace Polhem.Markdown.Model;

/// <summary>
/// A link at the end of a footnote back to one reference to it, <see cref="FootnoteReferenceNode.Id"/>. A note referenced
/// more than once has one back link per reference; <see cref="Position"/> counts them from 1, and from 2 on it is shown
/// after the arrow.
/// </summary>
internal sealed record FootnoteBackLinkNode(SourceRange Range, string ReferenceId, int Position) : InlineNode(Range);
