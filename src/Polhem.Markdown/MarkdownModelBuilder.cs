using System.Globalization;
using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Polhem.Markdown.Diagrams;
using Polhem.Markdown.Formulas;
using Polhem.Markdown.Highlighting;
using Polhem.Markdown.Model;

namespace Polhem.Markdown;

/// <summary>
/// Parses Markdown with Markdig and turns the syntax tree into the model the viewers render, applying the safety policy
/// on the way, so every viewer receives the same, already checked result.
/// </summary>
/// <remarks>
/// The policy: raw HTML is never passed on; a few elements README files use (images, links, emphasis, centered blocks,
/// collapsible sections) become nodes, any other tag is dropped and its content kept, and the content of scripts and
/// styles is dropped (see <see cref="Html.HtmlElements"/>); an image whose source is not an
/// allowed image scheme (see <see cref="MarkdownOptions.AllowedImageSchemes"/>) shows as its alternative text; a Mermaid
/// diagram is laid out here and drawn by the viewers from shapes, never by running a script; a link
/// whose target is not an absolute URI with an allowed scheme shows as its text, except a link to the id of a heading in
/// the same text (<c>#id</c>), which moves to that heading; a node this builder does not know shows as its source text.
/// Footnotes that are referenced show at the end, numbered by their first reference, as on GitHub; a reference to a note
/// that is not defined shows as written. Link reference definitions are not shown, as in any Markdown renderer, and
/// neither is YAML front matter. Inline syntax nested deeper than <see cref="MaxInlineDepth"/> levels, such as emphasis
/// inside emphasis, shows the rest as written, so the model stays shallow enough for every viewer to render.
/// </remarks>
internal sealed partial class MarkdownModelBuilder
{
    // NOTE: only the extensions whose nodes the viewers render are enabled. `UseAdvancedExtensions` would parse
    // definition lists and others that no viewer draws. Inline math follows GitHub's rules (`GitHubMathExtension`). Markdig reads raw HTML into HTML blocks and inlines, which
    // this builder turns into nodes (see `HtmlComposer`); no HTML ever reaches a viewer as markup.
    private static readonly MarkdownPipeline s_pipeline = Pipeline();

    private static MarkdownPipeline Pipeline()
    {
        var builder = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseAutoLinks()
            .UseCjkFriendlyEmphasis()
            .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
            .UseTaskLists()
            .UseAlertBlocks()
            .UseEmojiAndSmiley(enableSmileys: false)
            .UseYamlFrontMatter()
            .UseFootnotes()
            .UseMathematics()
            .UsePreciseSourceLocation();
        builder.Extensions.Add(new GitHubMathExtension());
        return builder.Build();
    }

    private readonly string _markdown;
    private readonly MarkdownOptions _options;
    private readonly Dictionary<HeadingBlock, string> _headingIds = [];
    private readonly HashSet<string> _ids = [];
    private readonly Dictionary<Footnote, int> _noteNumbers = [];
    private readonly Dictionary<int, int> _backLinkPositions = [];

    /// <summary>
    /// The deepest inline nesting the model keeps. Markdig nests emphasis as deep as the text asks, and the conversion and
    /// the viewers walk it by recursion, so a text of thousands of <c>*</c> overflowed the stack and ended the process.
    /// </summary>
    internal const int MaxInlineDepth = 32;

    /// <summary>How many inline containers the conversion is inside, to stop at <see cref="MaxInlineDepth"/>.</summary>
    private int _inlineDepth;

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
        builder.AssignHeadingIds(document);
        builder.NumberFootnotes(document);
        return new DocumentNode(builder.Blocks(document));
    }

    private ParagraphNode WholeTextAsParagraph()
    {
        var range = new SourceRange(0, _markdown.Length);
        return new ParagraphNode(range, [new TextNode(range, _markdown)]);
    }

    private List<BlockNode> Blocks(ContainerBlock container)
    {
        if (container.Any(block => block is HtmlBlock))
        {
            return BlocksWithHtml(container);
        }
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
        MathBlock math => FormulaBlockOrCode(Range(math), math.Lines.ToString()),
        ParagraphBlock { Inline: { FirstChild: MathInline { DelimiterCount: 2 } display } inline } paragraph
            when ReferenceEquals(inline.LastChild, display) => FormulaBlockOrCode(Range(paragraph), display.Content.ToString()),
        HeadingBlock heading => new HeadingNode(Range(heading), heading.Level, Inlines(heading.Inline), _headingIds.GetValueOrDefault(heading, "")),
        ParagraphBlock paragraph => new ParagraphNode(Range(paragraph), Inlines(paragraph.Inline)),
        // NOTE: front matter is metadata for tools, not part of the document, so it is hidden. GitHub shows it as a
        // table; hiding it is a deliberate difference. Markdig reads it as a kind of code block, so it comes first.
        YamlFrontMatterBlock => null,
        FencedCodeBlock fenced => FencedCode(fenced),
        CodeBlock code => new CodeBlockNode(Range(code), "", code.Lines.ToString(), []),
        AlertBlock alert => Alert(alert),
        QuoteBlock quote => new QuoteNode(Range(quote), Blocks(quote)),
        ListBlock list => List(list),
        Table table => Table(table),
        ThematicBreakBlock rule => new ThematicBreakNode(Range(rule)),
        FootnoteGroup group => Footnotes(group),
        LinkReferenceDefinitionGroup or BlankLineBlock => null,
        _ => new ParagraphNode(Range(block), [SourceText(block)]),
    };

    /// <summary>
    /// Gives every heading its id, in document order, before any link is converted, so a link can refer to a heading
    /// further down.
    /// </summary>
    private void AssignHeadingIds(MarkdownDocument document)
    {
        var slugger = new HeadingSlugger();
        foreach (var heading in document.Descendants<HeadingBlock>())
        {
            var id = slugger.Next(heading.Inline is null ? "" : PlainText(heading.Inline));
            _headingIds[heading] = id;
            if (id.Length > 0)
            {
                _ids.Add(id);
            }
        }
    }

    private BlockNode Alert(AlertBlock alert)
    {
        var blocks = Blocks(alert);
        var kindText = alert.Kind.ToString();
        if (AlertKindOf(kindText) is { } kind)
        {
            return new AlertNode(Range(alert), kind, blocks);
        }

        // Markdig accepts any word as the kind. GitHub shows a quote whose marker is not one of its five as an ordinary
        // quote with the marker as text, and so does this builder.
        var text = "[!" + kindText + "]";
        var range = Range(alert);
        var start = _markdown.IndexOf(text, range.Start, StringComparison.Ordinal);
        var marker = new TextNode(start < 0 ? new SourceRange(range.Start, 0) : new SourceRange(start, text.Length), text);
        if (blocks is [ParagraphNode first, ..])
        {
            blocks[0] = first with
            {
                Inlines = first.Inlines.Count == 0
                    ? [marker]
                    : [marker, new TextNode(new SourceRange(marker.Range.End, 0), " "), .. first.Inlines],
            };
        }
        else
        {
            blocks.Insert(0, new ParagraphNode(marker.Range, [marker]));
        }
        return new QuoteNode(Range(alert), blocks);
    }

    private static AlertKind? AlertKindOf(string kind) => kind switch
    {
        "NOTE" => AlertKind.Note,
        "TIP" => AlertKind.Tip,
        "IMPORTANT" => AlertKind.Important,
        "WARNING" => AlertKind.Warning,
        "CAUTION" => AlertKind.Caution,
        _ => null,
    };

    private BlockNode FencedCode(FencedCodeBlock fenced)
    {
        var language = fenced.Info ?? "";
        var code = fenced.Lines.ToString();
        if (string.Equals(language, "math", StringComparison.OrdinalIgnoreCase))
        {
            return FormulaBlockOrCode(Range(fenced), code);
        }
        if (_options.RenderMermaidDiagrams
            && string.Equals(language, "mermaid", StringComparison.OrdinalIgnoreCase)
            && MermaidDiagram.TryDraw(code, out var drawing))
        {
            return new DiagramNode(Range(fenced), drawing);
        }
        return new CodeBlockNode(Range(fenced), language, code, CodeHighlighter.Tokenize(language, code));
    }

    private ListNode List(ListBlock list)
    {
        var start = list.IsOrdered && int.TryParse(list.OrderedStart, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? number
            : 1;
        var items = list.OfType<ListItemBlock>().Select(ListItem).ToList();
        return new ListNode(Range(list), list.IsOrdered, start, list.IsLoose, items);
    }

    private ListItemNode ListItem(ListItemBlock item)
    {
        var blocks = Blocks(item);

        // The check box belongs to the item, so its inline is dropped from the text (see `AddInline`), and with it the
        // space that separates it from the text.
        if (item.FirstOrDefault() is ParagraphBlock { Inline.FirstChild: TaskList task } && blocks is [ParagraphNode first, ..])
        {
            blocks[0] = first with { Inlines = WithoutLeadingSpace(first.Inlines) };
            return new ListItemNode(Range(item), blocks, task.Checked);
        }
        return new ListItemNode(Range(item), blocks, null);
    }

    private static List<InlineNode> WithoutLeadingSpace(IReadOnlyList<InlineNode> inlines)
    {
        var result = inlines.ToList();
        if (result is [TextNode text, ..] && text.Text.Length > 0 && char.IsWhiteSpace(text.Text[0]))
        {
            var trimmed = text.Text.TrimStart();
            var removed = text.Text.Length - trimmed.Length;
            var range = new SourceRange(text.Range.Start + Math.Min(removed, text.Range.Length), Math.Max(text.Range.Length - removed, 0));
            if (trimmed.Length == 0)
            {
                result.RemoveAt(0);
            }
            else
            {
                result[0] = new TextNode(range, trimmed);
            }
        }
        return result;
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
        if (container is null)
        {
            return [];
        }
        if (_inlineDepth >= MaxInlineDepth)
        {
            return [SourceText(container)];
        }
        _inlineDepth++;
        try
        {
            var children = Flattened(container).ToList();
            if (children.Exists(inline => inline is HtmlInline))
            {
                return InlinesWithHtml(children);
            }
            var inlines = new List<InlineNode>();
            foreach (var inline in children)
            {
                AddInline(inline, inlines);
            }
            return inlines;
        }
        finally
        {
            _inlineDepth--;
        }
    }

    /// <summary>
    /// The inlines of <paramref name="container"/>, with the content of each unmatched <c>[</c> or <c>![</c> following it
    /// at the same level.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: Markdig leaves an unmatched bracket as a container around everything after it, so ten thousand of them
    /// are a tree that deep. Walking it here without recursion keeps the model, and every viewer that renders it, from
    /// overflowing the stack.
    /// </remarks>
    private static IEnumerable<Inline> Flattened(ContainerInline container)
    {
        var after = new Stack<Inline>();
        var current = container.FirstChild;
        while (current is not null || after.Count > 0)
        {
            current ??= after.Pop();
            yield return current;
            if (current is LinkDelimiterInline { FirstChild: { } first })
            {
                if (current.NextSibling is { } next)
                {
                    after.Push(next);
                }
                current = first;
            }
            else
            {
                current = current.NextSibling;
            }
        }
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
            case MathInline math:
                inlines.Add(FormulaOrCode(math));
                break;
            case EmphasisInline emphasis:
                inlines.Add(new EmphasisNode(Range(emphasis), Kind(emphasis), Inlines(emphasis)));
                break;
            case FootnoteLink { IsBackLink: false, Footnote: { } note } reference when _noteNumbers.ContainsKey(note):
                inlines.Add(new FootnoteReferenceNode(Range(reference), _noteNumbers[note], ReferenceId(reference), NoteId(note)));
                break;
            case FootnoteLink { IsBackLink: true } back:
                if (_backLinkPositions.TryGetValue(back.Index, out var position))
                {
                    inlines.Add(BackLink(back, position));
                }
                break;
            case TaskList task:
                if (!IsItemCheckBox(task))
                {
                    inlines.Add(SourceText(task));
                }
                break;
            case LineBreakInline lineBreak:
                inlines.Add(lineBreak.IsHard ? new LineBreakNode(Range(lineBreak)) : new TextNode(Range(lineBreak), " "));
                break;
            case AutolinkInline autolink:
                AddLink(Range(autolink), autolink.IsEmail ? "mailto:" + autolink.Url : autolink.Url, [new TextNode(Range(autolink), autolink.Url)], inlines);
                break;
            case LinkInline { IsImage: true } image:
                inlines.Add(Image(image));
                break;
            case LinkInline link:
                AddLink(Range(link), link.GetDynamicUrl?.Invoke() ?? link.Url, Inlines(link), inlines);
                break;
            case LinkDelimiterInline delimiter:
                // An unmatched bracket shows as written; its content follows it (see `Flattened`).
                inlines.Add(new TextNode(Range(delimiter), delimiter.IsImage ? "![" : "["));
                break;
            case HtmlEntityInline entity:
                inlines.Add(new TextNode(Range(entity), entity.Transcoded.ToString()));
                break;
            case ContainerInline container:
                inlines.AddRange(Inlines(container));
                break;
            default:
                inlines.Add(SourceText(inline));
                break;
        }
    }

    /// <summary>
    /// Whether a task list inline opens its list item, where it is the item's check box. Markdig also parses `[x]`
    /// later in an item's text, which shows as written, as it does on GitHub.
    /// </summary>
    private static bool IsItemCheckBox(TaskList task) =>
        task.PreviousSibling is null
        && task.Parent?.ParentBlock is ParagraphBlock { Parent: ListItemBlock item } paragraph
        && ReferenceEquals(item.FirstOrDefault(), paragraph);

    private static EmphasisKind Kind(EmphasisInline emphasis) => emphasis switch
    {
        { DelimiterChar: '~' } => EmphasisKind.Strikethrough,
        { DelimiterCount: >= 2 } => EmphasisKind.Bold,
        _ => EmphasisKind.Italic,
    };

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
