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
        _ => [],
    };
}
