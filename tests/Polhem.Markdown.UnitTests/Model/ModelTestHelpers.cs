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
        AnchorLinkNode anchor => Text(anchor.Inlines),
        ImageNode image => image.AlternativeText,
        FootnoteReferenceNode reference => "[" + reference.Number + "]",
        FootnoteBackLinkNode back => back.Position > 1 ? " \u21A9" + back.Position : " \u21A9",
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
            AlignedBlockNode aligned => aligned.Blocks,
            DetailsNode details => details.Summary.Concat<MarkdownNode>(details.Blocks),
            TableNode table => table.HeaderRows.Concat(table.Rows),
            TableRowNode row => row.Cells,
            TableCellNode cell => cell.Inlines,
            EmphasisNode emphasis => emphasis.Inlines,
            LinkNode link => link.Inlines,
            AnchorLinkNode anchor => anchor.Inlines,
            FootnotesNode footnotes => footnotes.Notes,
            FootnoteNode note => note.Blocks,
            CodeBlockNode or DiagramNode or ThematicBreakNode or TextNode or CodeSpanNode or LineBreakNode
                or FootnoteReferenceNode or FootnoteBackLinkNode or ImageNode => [],
            _ => throw new ArgumentOutOfRangeException(nameof(node), node.GetType().Name),
        };
        return children.SelectMany(Walk).Prepend(node);
    }
}
