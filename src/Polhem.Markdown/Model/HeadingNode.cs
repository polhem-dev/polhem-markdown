namespace Polhem.Markdown.Model;

/// <summary>
/// A heading; <see cref="Level"/> runs from 1 to 6. <see cref="Id"/> is the anchor GitHub gives the heading, which an
/// <see cref="AnchorLinkNode"/> in the same document refers to; it is empty when the heading text yields none.
/// </summary>
internal sealed record HeadingNode(SourceRange Range, int Level, IReadOnlyList<InlineNode> Inlines, string Id) : BlockNode(Range);
