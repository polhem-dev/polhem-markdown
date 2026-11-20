using System.Globalization;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Polhem.Markdown.Model;

namespace Polhem.Markdown;

/// <summary>
/// Parses Markdown with Markdig and turns the syntax tree into the model the viewers render, applying the safety policy
/// on the way, so every viewer receives the same, already checked result.
/// </summary>
/// <remarks>
/// The policy: raw HTML is never interpreted and shows as the text it was written as; an image shows as its alternative
/// text; a link whose target is not an absolute URI with an allowed scheme shows as its text; a node this builder does
/// not know shows as its source text. Link reference definitions are not shown, as in any Markdown renderer.
/// </remarks>
internal sealed class MarkdownModelBuilder
{
    // NOTE: only the extensions whose nodes the viewers render are enabled. `UseAdvancedExtensions` would parse
    // footnotes, task lists, math and others that no viewer draws. `DisableHtml` makes Markdig itself read raw HTML as
    // text, and the HTML cases below remain only as a second line of defence.
    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseAutoLinks()
        .UseCjkFriendlyEmphasis()
        .UsePreciseSourceLocation()
        .Build();

    private readonly string _markdown;
    private readonly MarkdownOptions _options;

    private MarkdownModelBuilder(string markdown, MarkdownOptions options)
    {
        _markdown = markdown;
        _options = options;
    }

    /// <summary>Parses <paramref name="markdown"/> into the model.</summary>
    public static DocumentNode Build(string markdown, MarkdownOptions options)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        ArgumentNullException.ThrowIfNull(options);

        var builder = new MarkdownModelBuilder(markdown, options);
        MarkdownDocument document;
        try
        {
            document = Markdig.Markdown.Parse(markdown, s_pipeline);
        }
        catch (ArgumentException)
        {
            // Markdig refuses a text nested deeper than its depth limit. The text comes from people and AI models,
            // so it is shown as written instead of failing the view.
            return new DocumentNode([builder.WholeTextAsParagraph()]);
        }
        return new DocumentNode(builder.Blocks(document));
    }

    private ParagraphNode WholeTextAsParagraph()
    {
        var range = new SourceRange(0, _markdown.Length);
        return new ParagraphNode(range, [new TextNode(range, _markdown)]);
    }

    private List<BlockNode> Blocks(ContainerBlock container)
    {
        var blocks = new List<BlockNode>(container.Count);
        foreach (var block in container)
        {
            if (Block(block) is { } node)
            {
                blocks.Add(node);
            }
        }
        return blocks;
    }

    private BlockNode? Block(Block block) => block switch
    {
        HeadingBlock heading => new HeadingNode(Range(heading), heading.Level, Inlines(heading.Inline)),
        ParagraphBlock paragraph => new ParagraphNode(Range(paragraph), Inlines(paragraph.Inline)),
        FencedCodeBlock fenced => new CodeBlockNode(Range(fenced), fenced.Info ?? "", fenced.Lines.ToString()),
        CodeBlock code => new CodeBlockNode(Range(code), "", code.Lines.ToString()),
        QuoteBlock quote => new QuoteNode(Range(quote), Blocks(quote)),
        ListBlock list => List(list),
        Table table => Table(table),
        ThematicBreakBlock rule => new ThematicBreakNode(Range(rule)),
        LinkReferenceDefinitionGroup or BlankLineBlock => null,
        _ => new ParagraphNode(Range(block), [SourceText(block)]),
    };

    private ListNode List(ListBlock list)
    {
        var start = list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : 1;
        var items = list.OfType<ListItemBlock>().Select(item => new ListItemNode(Range(item), Blocks(item))).ToList();
        return new ListNode(Range(list), list.IsOrdered, start, list.IsLoose, items);
    }

    private TableNode Table(Table table)
    {
        var headerRows = new List<TableRowNode>();
        var rows = new List<TableRowNode>();
        foreach (var row in table.OfType<TableRow>())
        {
            var cells = row.OfType<TableCell>().Select(cell => new TableCellNode(Range(cell), CellInlines(cell))).ToList();
            (row.IsHeader ? headerRows : rows).Add(new TableRowNode(Range(row), cells));
        }

        // NOTE: Markdig may keep one column definition more than any row has cells, so the column count comes from the
        // rows and the definitions only supply the alignment.
        var columns = headerRows.Concat(rows).Select(row => row.Cells.Count).DefaultIfEmpty(0).Max();
        var alignments = Enumerable.Range(0, columns)
            .Select(index => index < table.ColumnDefinitions.Count ? Alignment(table.ColumnDefinitions[index].Alignment) : ColumnAlignment.None)
            .ToList();

        return new TableNode(Range(table), alignments, headerRows, rows);
    }

    private static ColumnAlignment Alignment(TableColumnAlign? alignment) => alignment switch
    {
        TableColumnAlign.Left => ColumnAlignment.Left,
        TableColumnAlign.Center => ColumnAlignment.Center,
        TableColumnAlign.Right => ColumnAlignment.Right,
        _ => ColumnAlignment.None,
    };

    private List<InlineNode> CellInlines(TableCell cell)
    {
        var inlines = new List<InlineNode>();
        foreach (var paragraph in cell.OfType<ParagraphBlock>())
        {
            inlines.AddRange(Inlines(paragraph.Inline));
        }
        return inlines;
    }

    private List<InlineNode> Inlines(ContainerInline? container)
    {
        var inlines = new List<InlineNode>();
        if (container is null)
        {
            return inlines;
        }
        foreach (var inline in container)
        {
            AddInline(inline, inlines);
        }
        return inlines;
    }

    private void AddInline(Inline inline, List<InlineNode> inlines)
    {
        switch (inline)
        {
            case LiteralInline literal:
                inlines.Add(new TextNode(Range(literal), literal.Content.ToString()));
                break;
            case CodeInline code:
                inlines.Add(new CodeSpanNode(Range(code), code.Content));
                break;
            case EmphasisInline emphasis:
                inlines.Add(new EmphasisNode(Range(emphasis), emphasis.DelimiterCount >= 2, Inlines(emphasis)));
                break;
            case LineBreakInline lineBreak:
                inlines.Add(lineBreak.IsHard ? new LineBreakNode(Range(lineBreak)) : new TextNode(Range(lineBreak), " "));
                break;
            case AutolinkInline autolink:
                AddLink(autolink, autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url, [new TextNode(Range(autolink), autolink.Url)], inlines);
                break;
            case LinkInline { IsImage: true } image:
                inlines.Add(new TextNode(Range(image), PlainText(image)));
                break;
            case LinkInline link:
                AddLink(link, link.GetDynamicUrl?.Invoke() ?? link.Url, Inlines(link), inlines);
                break;
            case HtmlEntityInline entity:
                inlines.Add(new TextNode(Range(entity), entity.Transcoded.ToString()));
                break;
            case HtmlInline html:
                inlines.Add(new TextNode(Range(html), html.Tag));
                break;
            case ContainerInline container:
                inlines.AddRange(Inlines(container));
                break;
            default:
                inlines.Add(SourceText(inline));
                break;
        }
    }

    private void AddLink(Inline link, string? target, List<InlineNode> content, List<InlineNode> inlines)
    {
        if (!TryCreateTarget(target, out var uri))
        {
            inlines.AddRange(content);
            return;
        }
        if (content.Count == 0)
        {
            content.Add(new TextNode(Range(link), target!));
        }
        inlines.Add(new LinkNode(Range(link), uri, content));
    }

    private bool TryCreateTarget(string? target, out Uri uri)
    {
        // WARNING: on Unix `Uri.TryCreate` accepts a rooted path such as `/etc/passwd` as an absolute `file` URI, so
        // the scheme check below is what refuses it; do not relax it to "any absolute URI".
        if (Uri.TryCreate(target, UriKind.Absolute, out var created) && _options.AllowedLinkSchemes.Contains(created.Scheme))
        {
            uri = created;
            return true;
        }
        uri = null!;
        return false;
    }

    private static string PlainText(ContainerInline container)
    {
        var text = new System.Text.StringBuilder();
        foreach (var descendant in container.Descendants())
        {
            switch (descendant)
            {
                case LiteralInline literal:
                    text.Append(literal.Content.ToString());
                    break;
                case CodeInline code:
                    text.Append(code.Content);
                    break;
                case LineBreakInline:
                    text.Append(' ');
                    break;
            }
        }
        return text.ToString();
    }

    private TextNode SourceText(MarkdownObject node)
    {
        var range = Range(node);
        return new TextNode(range, _markdown.Substring(range.Start, range.Length));
    }

    private SourceRange Range(MarkdownObject node)
    {
        var span = node.Span;
        if (span.IsEmpty || span.Start < 0 || span.Start >= _markdown.Length)
        {
            return new SourceRange(Math.Clamp(span.Start, 0, _markdown.Length), 0);
        }
        var end = Math.Min(span.End + 1, _markdown.Length);

        // Markdig ends some blocks (a list, for one) after their newline. A block's range stops at its last visible
        // character, so ranges compare the same however the block was terminated. An inline keeps its newline: a line
        // break is nothing else.
        if (node is Block)
        {
            while (end > span.Start && _markdown[end - 1] is '\n' or '\r')
            {
                end--;
            }
        }
        return new SourceRange(span.Start, end - span.Start);
    }
}
