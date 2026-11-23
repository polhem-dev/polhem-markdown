using System.Globalization;
using Markdig;
using Markdig.Extensions.Alerts;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Polhem.Markdown.Diagrams;
using Polhem.Markdown.Model;

namespace Polhem.Markdown;

/// <summary>
/// Parses Markdown with Markdig and turns the syntax tree into the model the viewers render, applying the safety policy
/// on the way, so every viewer receives the same, already checked result.
/// </summary>
/// <remarks>
/// The policy: raw HTML is never interpreted and shows as the text it was written as; an image shows as its alternative
/// text; a Mermaid diagram is laid out here and drawn by the viewers from shapes, never by running a script; a link
/// whose target is not an absolute URI with an allowed scheme shows as its text, except a link to the id of a heading in
/// the same text (<c>#id</c>), which moves to that heading; a node this builder does not know shows as its source text.
/// Footnotes that are referenced show at the end, numbered by their first reference, as on GitHub; a reference to a note
/// that is not defined shows as written. Link reference definitions are not shown, as in any Markdown renderer, and
/// neither is YAML front matter.
/// </remarks>
internal sealed class MarkdownModelBuilder
{
    // NOTE: only the extensions whose nodes the viewers render are enabled. `UseAdvancedExtensions` would parse
    // math, definition lists and others that no viewer draws. `DisableHtml` makes Markdig itself read raw HTML as
    // text, and the HTML cases below remain only as a second line of defence.
    private static readonly MarkdownPipeline s_pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .UseAutoLinks()
        .UseCjkFriendlyEmphasis()
        .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
        .UseTaskLists()
        .UseAlertBlocks()
        .UseEmojiAndSmiley(enableSmileys: false)
        .UseYamlFrontMatter()
        .UseFootnotes()
        .UsePreciseSourceLocation()
        .Build();

    private readonly string _markdown;
    private readonly MarkdownOptions _options;
    private readonly Dictionary<HeadingBlock, string> _headingIds = [];
    private readonly HashSet<string> _ids = [];
    private readonly Dictionary<Footnote, int> _noteNumbers = [];
    private readonly Dictionary<int, int> _backLinkPositions = [];

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
        HeadingBlock heading => new HeadingNode(Range(heading), heading.Level, Inlines(heading.Inline), _headingIds.GetValueOrDefault(heading, "")),
        ParagraphBlock paragraph => new ParagraphNode(Range(paragraph), Inlines(paragraph.Inline)),
        // NOTE: front matter is metadata for tools, not part of the document, so it is hidden. GitHub shows it as a
        // table; hiding it is a deliberate difference. Markdig reads it as a kind of code block, so it comes first.
        YamlFrontMatterBlock => null,
        FencedCodeBlock fenced => FencedCode(fenced),
        CodeBlock code => new CodeBlockNode(Range(code), "", code.Lines.ToString()),
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
        if (_options.RenderMermaidDiagrams
            && string.Equals(language, "mermaid", StringComparison.OrdinalIgnoreCase)
            && MermaidDiagram.TryDraw(code, out var drawing))
        {
            return new DiagramNode(Range(fenced), drawing);
        }
        return new CodeBlockNode(Range(fenced), language, code);
    }

    /// <summary>
    /// The referenced notes, in number order. Markdig numbers them by first reference, drops the ones nothing refers to,
    /// and ends each note with a back link per reference.
    /// </summary>
    private FootnotesNode? Footnotes(FootnoteGroup group)
    {
        var notes = group.OfType<Footnote>()
            .Where(_noteNumbers.ContainsKey)
            .OrderBy(note => _noteNumbers[note])
            .Select(note => new FootnoteNode(Range(note), _noteNumbers[note], NoteId(note), Blocks(note)))
            .ToList();
        if (notes.Count == 0)
        {
            return null;
        }
        var start = notes.Min(note => note.Range.Start);
        return new FootnotesNode(new SourceRange(start, notes.Max(note => note.Range.End) - start), notes);
    }

    /// <summary>
    /// Numbers the notes a reader can reach, in the order they are first reached: references in the text, in order, then
    /// references inside the notes so numbered. Each back link gets its position among its note's shown references.
    /// Every note and every reference is visited once.
    /// </summary>
    /// <remarks>
    /// Markdig's own numbers count references this builder does not show: one in an image's alternative text, which
    /// shows as plain text, and one inside a note that is not shown. Either would leave a note, or a back link, that
    /// nothing in the text leads to.
    /// </remarks>
    private void NumberFootnotes(MarkdownDocument document)
    {
        var hidden = document.Descendants<LinkInline>()
            .Where(link => link.IsImage)
            .SelectMany(image => image.Descendants<FootnoteLink>())
            .Select(link => link.Index)
            .ToHashSet();
        var shown = new HashSet<int>();

        // The text comes first; each note joins the queue when it is first reached, so its own references are read after
        // everything reached before it.
        var pending = new Queue<IEnumerable<FootnoteLink>>();
        pending.Enqueue(document.Where(block => block is not FootnoteGroup).SelectMany(FootnoteLinks));
        while (pending.TryDequeue(out var links))
        {
            foreach (var link in links)
            {
                if (link.IsBackLink || link.Footnote is not { } note || hidden.Contains(link.Index))
                {
                    continue;
                }
                shown.Add(link.Index);
                if (_noteNumbers.TryAdd(note, _noteNumbers.Count + 1))
                {
                    pending.Enqueue(FootnoteLinks(note));
                }
            }
        }

        foreach (var note in _noteNumbers.Keys)
        {
            var position = 0;
            foreach (var link in note.Links.Where(link => shown.Contains(link.Index)))
            {
                _backLinkPositions[link.Index] = ++position;
            }
        }
    }

    /// <summary>The footnote links in a block and the blocks inside it, in document order.</summary>
    /// <remarks>Markdig's block descendants stop at blocks, so the inlines of each leaf block are walked here.</remarks>
    private static IEnumerable<FootnoteLink> FootnoteLinks(Block block) => block switch
    {
        LeafBlock { Inline: { } inline } => inline.Descendants<FootnoteLink>(),
        ContainerBlock container => container.SelectMany(FootnoteLinks),
        _ => [],
    };

    // NOTE: a colon never appears in a heading id (see `HeadingSlugger`), so these ids cannot match a heading.
    private string NoteId(Footnote note) => "fn:" + _noteNumbers[note].ToString(CultureInfo.InvariantCulture);

    private static string ReferenceId(FootnoteLink link) => "fnref:" + link.Index.ToString(CultureInfo.InvariantCulture);

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

    /// <summary>
    /// A back link from a note to one of its references. Markdig adds it with no source of its own, so it is placed at the
    /// end of the paragraph that holds it.
    /// </summary>
    private FootnoteBackLinkNode BackLink(FootnoteLink back, int position)
    {
        var end = back.Parent?.ParentBlock is { } paragraph ? Range(paragraph).End : 0;
        return new FootnoteBackLinkNode(new SourceRange(end, 0), ReferenceId(back), position);
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

    private void AddLink(Inline link, string? target, List<InlineNode> content, List<InlineNode> inlines)
    {
        if (target is ['#', ..])
        {
            AddAnchorLink(link, target, content, inlines);
            return;
        }
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

    /// <summary>
    /// Adds a link to a heading of this text, or only its content when no heading has the id. The id may be written
    /// percent-encoded, as GitHub writes a link to a heading with non-ASCII letters.
    /// </summary>
    private void AddAnchorLink(Inline link, string target, List<InlineNode> content, List<InlineNode> inlines)
    {
        var id = Uri.UnescapeDataString(target[1..]);
        if (!_ids.Contains(id))
        {
            inlines.AddRange(content);
            return;
        }
        if (content.Count == 0)
        {
            content.Add(new TextNode(Range(link), target));
        }
        inlines.Add(new AnchorLinkNode(Range(link), id, content));
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
