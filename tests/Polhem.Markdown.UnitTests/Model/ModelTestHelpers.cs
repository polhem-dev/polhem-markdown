using Polhem.Markdown.Model;

namespace Polhem.Markdown.UnitTests.Model;

internal static class ModelTestHelpers
{
    public static DocumentNode Parse(string markdown, MarkdownOptions? options = null) =>
        MarkdownModelBuilder.Build(markdown, options ?? new MarkdownOptions());

    public static T Single<T>(DocumentNode document) where T : BlockNode => Assert.IsType<T>(Assert.Single(document.Blocks));

    /// <summary>Every node of the document, depth first.</summary>
    public static IEnumerable<MarkdownNode> AllNodes(DocumentNode document) => document.Blocks.SelectMany(Walk);

    /// <summary>The visible text of some inlines, as a viewer would show it.</summary>
    public static string Text(IEnumerable<InlineNode> inlines) => string.Concat(inlines.Select(inline => inline switch
    {
        TextNode text => text.Text,
        CodeSpanNode code => code.Code,
        EmphasisNode emphasis => Text(emphasis.Inlines),
        LinkNode link => Text(link.Inlines),
        LineBreakNode => "\n",
        _ => throw new ArgumentOutOfRangeException(nameof(inlines), inline.GetType().Name),
    }));

    private static IEnumerable<MarkdownNode> Walk(MarkdownNode node)
    {
        IEnumerable<MarkdownNode> children = node switch
        {
            ParagraphNode paragraph => paragraph.Inlines,
            HeadingNode heading => heading.Inlines,
            ListNode list => list.Items,
            ListItemNode item => item.Blocks,
            QuoteNode quote => quote.Blocks,
            AlertNode alert => alert.Blocks,
            TableNode table => table.HeaderRows.Concat(table.Rows),
            TableRowNode row => row.Cells,
            TableCellNode cell => cell.Inlines,
            EmphasisNode emphasis => emphasis.Inlines,
            LinkNode link => link.Inlines,
            CodeBlockNode or ThematicBreakNode or TextNode or CodeSpanNode or LineBreakNode => [],
            _ => throw new ArgumentOutOfRangeException(nameof(node), node.GetType().Name),
        };
        return children.SelectMany(Walk).Prepend(node);
    }
}
