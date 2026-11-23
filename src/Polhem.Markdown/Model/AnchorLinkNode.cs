namespace Polhem.Markdown.Model;

/// <summary>
/// A link to a heading of the same document, written <c>[text](#id)</c>. It exists only when a heading has that
/// <see cref="HeadingNode.Id"/>; the viewer moves to the heading itself instead of reporting the link to the host.
/// </summary>
internal sealed record AnchorLinkNode(SourceRange Range, string Id, IReadOnlyList<InlineNode> Inlines) : InlineNode(Range);
