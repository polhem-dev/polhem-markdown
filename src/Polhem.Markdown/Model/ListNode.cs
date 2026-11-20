namespace Polhem.Markdown.Model;

/// <summary>
/// A bulleted or numbered list. <see cref="Start"/> is the number of the first item of a numbered list. A loose list has
/// blank lines between its items, so a viewer leaves more space between them.
/// </summary>
internal sealed record ListNode(SourceRange Range, bool IsOrdered, int Start, bool IsLoose, IReadOnlyList<ListItemNode> Items)
    : BlockNode(Range);
