namespace Polhem.Markdown.Model;

/// <summary>
/// A section the reader opens and closes, from HTML <c>&lt;details&gt;</c>: <see cref="Summary"/> is always shown, and
/// <see cref="Blocks"/> when it is open. <see cref="IsOpen"/> is how it starts, from the <c>open</c> attribute. Without a
/// <c>&lt;summary&gt;</c> the summary is the word "Details", as a browser shows it.
/// </summary>
internal sealed record DetailsNode(SourceRange Range, IReadOnlyList<InlineNode> Summary, IReadOnlyList<BlockNode> Blocks, bool IsOpen)
    : BlockNode(Range);
