namespace Polhem.Markdown.Model;

/// <summary>Walks the model.</summary>
internal static class NodeTree
{
    /// <summary><paramref name="node"/> and every node inside it, depth first, in document order.</summary>
    public static IEnumerable<MarkdownNode> SelfAndDescendants(MarkdownNode node)
    {
        var stack = new Stack<MarkdownNode>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            yield return current;
            var children = Children(current);
            for (var index = children.Count - 1; index >= 0; index--)
            {
                stack.Push(children[index]);
            }
        }
    }

    /// <summary>
    /// The id a link in the text can move to <paramref name="node"/> by: a heading's, a footnote's, or a footnote
    /// reference's, which a back link returns to. Any other node has none.
    /// </summary>
    public static string? AnchorId(MarkdownNode node) => node switch
    {
        HeadingNode { Id.Length: > 0 } heading => heading.Id,
        FootnoteNode note => note.Id,
        FootnoteReferenceNode reference => reference.Id,
        _ => null,
    };

    private static IReadOnlyList<MarkdownNode> Children(MarkdownNode node) => node switch
    {
        ParagraphNode paragraph => paragraph.Inlines,
        HeadingNode heading => heading.Inlines,
        ListNode list => list.Items,
        ListItemNode item => item.Blocks,
        QuoteNode quote => quote.Blocks,
        AlertNode alert => alert.Blocks,
        TableNode table => [.. table.HeaderRows, .. table.Rows],
        TableRowNode row => row.Cells,
        TableCellNode cell => cell.Inlines,
        EmphasisNode emphasis => emphasis.Inlines,
        LinkNode link => link.Inlines,
        AnchorLinkNode anchor => anchor.Inlines,
        FootnotesNode footnotes => footnotes.Notes,
        FootnoteNode note => note.Blocks,
        _ => [],
    };
}
