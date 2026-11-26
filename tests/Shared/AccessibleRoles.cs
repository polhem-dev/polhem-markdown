using Polhem.Markdown.Model;

namespace Polhem.Markdown.Tests.Shared;

/// <summary>What an assistive technology is told an element of a rendered text is.</summary>
internal enum AccessibleRole
{
    /// <summary>Nothing of its own: the node is part of the text around it, or neither viewer describes it.</summary>
    None,
    Text,
    Heading,
    Link,
    List,
    ListItem,
    Table,
    HeaderCell,
    Cell,
    Image,
    Group,
    Disclosure,
    Separator,
    Formula,

    /// <summary>A code block: its text, and a copy button named by the viewer's label.</summary>
    Code,
}

/// <summary>
/// The role each node type of the model is shown with, and a text that contains it. Each viewer's accessibility test
/// renders every text and checks that it shows an element of that role, so the two viewers tell an assistive technology
/// the same thing, and a node type added to the model fails both until it has a role here
/// (polhem-dev/polhem-markdown#70).
/// </summary>
internal static class AccessibleRoles
{
    public static IReadOnlyDictionary<Type, (string Markdown, AccessibleRole Role)> ByNodeType { get; } =
        new Dictionary<Type, (string, AccessibleRole)>
        {
            [typeof(ParagraphNode)] = ("A paragraph.", AccessibleRole.Text),
            [typeof(HeadingNode)] = ("## A heading", AccessibleRole.Heading),
            [typeof(TextNode)] = ("text", AccessibleRole.None),
            [typeof(EmphasisNode)] = ("*text*", AccessibleRole.None),
            [typeof(CodeSpanNode)] = ("`code`", AccessibleRole.None),
            [typeof(LineBreakNode)] = ("one  \ntwo", AccessibleRole.None),
            [typeof(LinkNode)] = ("[a link](https://example.org)", AccessibleRole.Link),
            [typeof(AnchorLinkNode)] = ("# Top\n\n[back](#top)", AccessibleRole.Link),
            [typeof(ImageNode)] = ("![a cat](https://example.org/cat.png)", AccessibleRole.Image),
            [typeof(MathNode)] = ("$x^2$", AccessibleRole.Formula),
            [typeof(MathBlockNode)] = ("$$\nx^2\n$$", AccessibleRole.Formula),
            [typeof(ListNode)] = ("- one\n- two", AccessibleRole.List),
            [typeof(ListItemNode)] = ("1. one\n2. two", AccessibleRole.ListItem),
            [typeof(CodeBlockNode)] = ("```cs\nvar x = 1;\n```", AccessibleRole.Code),

            // NOTE: a diagram is a drawing in both viewers, with nothing that describes it yet.
            [typeof(DiagramNode)] = ("```mermaid\nflowchart LR\n  A --> B\n```", AccessibleRole.None),
            [typeof(QuoteNode)] = ("> A quote.", AccessibleRole.Group),
            [typeof(AlertNode)] = ("> [!NOTE]\n> An alert.", AccessibleRole.Group),
            [typeof(AlignedBlockNode)] = ("<p align=\"center\">centered</p>", AccessibleRole.None),
            [typeof(DetailsNode)] = ("<details>\n<summary>More</summary>\n\nHidden.\n\n</details>", AccessibleRole.Disclosure),
            [typeof(TableNode)] = ("| A | B |\n|---|---|\n| 1 | 2 |", AccessibleRole.Table),

            // NOTE: Avalonia's automation has no row type, so a row is checked through its cells only.
            [typeof(TableRowNode)] = ("| A | B |\n|---|---|\n| 1 | 2 |", AccessibleRole.HeaderCell),
            [typeof(TableCellNode)] = ("| A | B |\n|---|---|\n| 1 | 2 |", AccessibleRole.Cell),
            [typeof(ThematicBreakNode)] = ("***", AccessibleRole.Separator),
            [typeof(FootnotesNode)] = ("Text[^1].\n\n[^1]: A note.", AccessibleRole.List),
            [typeof(FootnoteNode)] = ("Text[^1].\n\n[^1]: A note.", AccessibleRole.ListItem),
            [typeof(FootnoteReferenceNode)] = ("Text[^1].\n\n[^1]: A note.", AccessibleRole.Link),
            [typeof(FootnoteBackLinkNode)] = ("Text[^1].\n\n[^1]: A note.", AccessibleRole.Link),
        };
}
