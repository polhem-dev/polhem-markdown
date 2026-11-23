using Polhem.Markdown;
using Polhem.Markdown.Model;

// Parses a text with every kind of node the model has and checks that each one came out.
const string Markdown = """
    # Heading

    A paragraph with *italic*, **bold**, `code`, a [link](https://example.com/), <https://example.org/>,
    a hard break\
    and <b>raw HTML</b> &amp; ![an image](https://example.com/i.png).

    1. one
    2. two
       - nested
       - [x] a done task, ~~struck~~

    > quoted

    > [!TIP]
    > An alert.

    ```csharp
    var x = 1;
    ```

    | a | b |
    |:--|--:|
    | 1 | 2 |

    ---
    """;

var document = MarkdownModelBuilder.Build(Markdown, new MarkdownOptions());

var found = new HashSet<Type>();
foreach (var block in document.Blocks)
{
    Collect(block);
}

Type[] expected =
[
    typeof(HeadingNode), typeof(ParagraphNode), typeof(ListNode), typeof(ListItemNode), typeof(QuoteNode), typeof(AlertNode),
    typeof(CodeBlockNode), typeof(TableNode), typeof(TableRowNode), typeof(TableCellNode), typeof(ThematicBreakNode),
    typeof(TextNode), typeof(EmphasisNode), typeof(CodeSpanNode), typeof(LinkNode), typeof(LineBreakNode),
];
var missing = expected.Where(type => !found.Contains(type)).Select(type => type.Name).ToList();
if (missing.Count > 0)
{
    Console.Error.WriteLine("Missing nodes: " + string.Join(", ", missing));
    return 1;
}

Console.WriteLine($"Polhem.Markdown AOT smoke test passed ({found.Count} node types).");
return 0;

void Collect(MarkdownNode node)
{
    found.Add(node.GetType());
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
        _ => [],
    };
    foreach (var child in children)
    {
        Collect(child);
    }
}
