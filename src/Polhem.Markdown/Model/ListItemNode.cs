namespace Polhem.Markdown.Model;

/// <summary>
/// An item of a list. <see cref="IsChecked"/> is <see langword="null"/> for an ordinary item, and tells whether a task
/// list item (<c>- [ ]</c> or <c>- [x]</c>) is done; its check box is not part of the blocks.
/// </summary>
internal sealed record ListItemNode(SourceRange Range, IReadOnlyList<BlockNode> Blocks, bool? IsChecked) : BlockNode(Range);
