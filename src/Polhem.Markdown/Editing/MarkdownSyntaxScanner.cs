using Markdig.Extensions.Alerts;
using Markdig.Extensions.Footnotes;
using Markdig.Extensions.Mathematics;
using Markdig.Extensions.Tables;
using Markdig.Extensions.TaskLists;
using Markdig.Extensions.Yaml;
using Markdig.Helpers;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Polhem.Markdown.Highlighting;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Editing;

/// <summary>
/// Finds the syntax of a Markdown source for the editor to color: markers, headings, emphasis, code, links, raw HTML and
/// formulas, and the tokens of fenced code in the languages <see cref="CodeHighlighter"/> knows, so code looks the same in
/// the editor and the viewers.
/// </summary>
/// <remarks>
/// It reads the source with the same configuration as the model the viewers render (<see cref="MarkdownParser"/>), but
/// keeps what that model drops: the position of every marker. A text Markdig refuses for being nested too deeply gives no
/// spans, so it shows as plain text, as the viewers show it as written.
/// </remarks>
internal sealed class MarkdownSyntaxScanner
{
    private readonly string _markdown;
    private readonly SyntaxPainter _painter;

    /// <summary>
    /// The nodes still to visit. Each node paints its own syntax and queues its children here, which paints a parent
    /// before its children, as nesting needs.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: the tree is walked with this stack, not by recursion. Markdig nests an unmatched <c>[</c> around
    /// everything after it, so a text of ten thousand of them is a tree that deep, and recursion overflowed the stack and
    /// ended the process.
    /// </remarks>
    private readonly Stack<MarkdownObject> _pending = new();

    /// <summary>Where each line of the source starts, so finding a line costs a search rather than a scan.</summary>
    private readonly List<int> _lineStarts = [0];

    private MarkdownSyntaxScanner(string markdown)
    {
        _markdown = markdown;
        _painter = new SyntaxPainter(markdown.Length);
        for (var index = 0; index < markdown.Length; index++)
        {
            if (markdown[index] == '\n' || (markdown[index] == '\r' && (index + 1 == markdown.Length || markdown[index + 1] != '\n')))
            {
                _lineStarts.Add(index + 1);
            }
        }
    }

    /// <summary>The spans of <paramref name="markdown"/>, in order and not overlapping, leaving out plain text.</summary>
    public static IReadOnlyList<SyntaxSpan> Scan(string markdown)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        if (MarkdownParser.Parse(markdown) is not { } document)
        {
            return [];
        }
        var scanner = new MarkdownSyntaxScanner(markdown);
        scanner.Blocks(document);
        while (scanner._pending.TryPop(out var node))
        {
            if (node is Block block)
            {
                scanner.Block(block);
            }
            else if (node is Inline inline)
            {
                scanner.Inline(inline);
            }
        }
        return scanner._painter.Runs();
    }

    /// <summary>Queues the blocks of <paramref name="container"/>.</summary>
    private void Blocks(ContainerBlock container)
    {
        foreach (var block in container)
        {
            _pending.Push(block);
        }
    }

    private void Block(Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                Heading(heading);
                break;
            case ParagraphBlock paragraph:
                Inlines(paragraph.Inline);
                break;
            case YamlFrontMatterBlock front:
                Fenced(front, front.Lines, "yaml", MarkdownSyntaxKind.Code);
                break;
            // A math block is a kind of fenced code block, so it comes first.
            case MathBlock math:
                Fenced(math, math.Lines, "", MarkdownSyntaxKind.Math);
                break;
            case FencedCodeBlock fenced:
                FencedCode(fenced);
                break;
            case CodeBlock code:
                Lines(code.Lines, "", MarkdownSyntaxKind.Code);
                break;
            case HtmlBlock html:
                Lines(html.Lines, "html", MarkdownSyntaxKind.Html);
                break;
            case AlertBlock alert:
                QuoteMarkers(alert);
                AlertMarker(alert);
                Blocks(alert);
                break;
            case QuoteBlock quote:
                QuoteMarkers(quote);
                Blocks(quote);
                break;
            case ListBlock list:
                Blocks(list);
                break;
            case ListItemBlock item:
                ListMarker(item);
                Blocks(item);
                break;
            case Table table:
                Table(table);
                break;
            case ThematicBreakBlock rule:
                _painter.Paint(Range(rule), MarkdownSyntaxKind.Marker);
                break;
            case Footnote note:
                _painter.Paint(FootnoteLabel(note.LabelSpan), MarkdownSyntaxKind.Marker);
                Blocks(note);
                break;
            // A footnote is also registered as a link reference definition, with no positions of its own.
            case FootnoteLinkReferenceDefinition:
                break;
            case LinkReferenceDefinition definition:
                LinkReferenceDefinition(definition);
                break;
            case ContainerBlock container:
                Blocks(container);
                break;
        }
    }

    private void Heading(HeadingBlock heading)
    {
        if (heading.IsSetext)
        {
            SetextHeading(Range(heading));
        }
        else
        {
            AtxHeading(heading);
        }
        Inlines(heading.Inline);
    }

    /// <summary>A heading underlined with <c>===</c> or <c>---</c>: the underline is the line after the text.</summary>
    private void SetextHeading(SourceRange range)
    {
        _painter.Paint(range, MarkdownSyntaxKind.Heading);
        var underline = _markdown.LastIndexOfAny(['\n', '\r'], Math.Max(range.End - 1, range.Start));
        if (underline >= range.Start)
        {
            _painter.Paint(new SourceRange(underline + 1, range.End - underline - 1), MarkdownSyntaxKind.Marker);
        }
    }

    /// <summary>A heading opened with <c>#</c>s, and optionally closed with them after the text.</summary>
    private void AtxHeading(HeadingBlock heading)
    {
        // Markdig ends an ATX heading at its text; the line goes on with the optional closing #s.
        var start = Range(heading).Start;
        var range = new SourceRange(start, LineEnd(start) - start);
        _painter.Paint(range, MarkdownSyntaxKind.Heading);

        var opening = Skip(range.Start, range.End, ' ');
        var hashes = Skip(opening, range.End, '#');
        _painter.Paint(new SourceRange(opening, hashes - opening), MarkdownSyntaxKind.Marker);

        var textEnd = heading.Inline is { Span.IsEmpty: false } inline ? Range(inline).End : hashes;
        var closingEnd = range.End;
        while (closingEnd > textEnd && _markdown[closingEnd - 1] is ' ' or '\t')
        {
            closingEnd--;
        }
        var closingStart = closingEnd;
        while (closingStart > textEnd && _markdown[closingStart - 1] is '#')
        {
            closingStart--;
        }
        _painter.Paint(new SourceRange(closingStart, closingEnd - closingStart), MarkdownSyntaxKind.Marker);
    }

    /// <summary>The first position from <paramref name="start"/> before <paramref name="end"/> that is not <paramref name="character"/>.</summary>
    private int Skip(int start, int end, char character)
    {
        while (start < end && _markdown[start] == character)
        {
            start++;
        }
        return start;
    }

    private void FencedCode(FencedCodeBlock fenced)
    {
        var language = fenced.Info ?? "";
        var kind = string.Equals(language, "math", StringComparison.OrdinalIgnoreCase) ? MarkdownSyntaxKind.Math : MarkdownSyntaxKind.Code;
        Fenced(fenced, fenced.Lines, language, kind);
    }

    /// <summary>A block between fences: the fences and the info string are markers, the lines inside are content.</summary>
    private void Fenced(Block block, StringLineGroup lines, string language, MarkdownSyntaxKind kind)
    {
        _painter.Paint(Range(block), MarkdownSyntaxKind.Marker);
        Lines(lines, language, kind);
    }

    /// <summary>
    /// Paints the lines of a block as <paramref name="kind"/> and the tokens <see cref="CodeHighlighter"/> finds in them
    /// over it. The tokens are positions in the lines joined by newlines, which this maps back to the source line by line,
    /// since a block inside a list or a quote leaves out the indentation and markers that start each line.
    /// </summary>
    /// <remarks>
    /// A line's slice points into the source unless Markdig had to rewrite the line (a tab inside the indentation, for
    /// one); the line then starts at its position, which is where the source line starts.
    /// </remarks>
    private void Lines(StringLineGroup lines, string language, MarkdownSyntaxKind kind)
    {
        var count = lines.Count;
        var starts = new int[count];
        var lengths = new int[count];
        var positions = new int[count];
        var joined = 0;
        for (var index = 0; index < count; index++)
        {
            var source = lines.Lines[index];
            starts[index] = joined;
            lengths[index] = source.Slice.Length;
            positions[index] = ReferenceEquals(source.Slice.Text, _markdown) ? source.Slice.Start : source.Position;
            _painter.Paint(new SourceRange(positions[index], lengths[index]), kind);
            joined += source.Slice.Length + 1;
        }
        if (language.Length == 0)
        {
            return;
        }

        var line = 0;
        foreach (var token in CodeHighlighter.Tokenize(language, lines.ToString()))
        {
            var tokenKind = Kind(token.Kind);
            var start = token.Start;
            while (start < token.End && line < count)
            {
                var lineEnd = starts[line] + lengths[line];
                if (start >= lineEnd)
                {
                    line++;
                    start = Math.Max(start, line < count ? starts[line] : start);
                    continue;
                }
                var end = Math.Min(token.End, lineEnd);
                _painter.Paint(new SourceRange(positions[line] + start - starts[line], end - start), tokenKind);
                start = end;
            }
        }
    }

    private static MarkdownSyntaxKind Kind(CodeTokenKind kind) => kind switch
    {
        CodeTokenKind.Keyword => MarkdownSyntaxKind.CodeKeyword,
        CodeTokenKind.String => MarkdownSyntaxKind.CodeString,
        CodeTokenKind.Comment => MarkdownSyntaxKind.CodeComment,
        CodeTokenKind.Constant => MarkdownSyntaxKind.CodeConstant,
        CodeTokenKind.Tag => MarkdownSyntaxKind.CodeTag,
        _ => MarkdownSyntaxKind.CodeProperty,
    };

    /// <summary>
    /// The <c>&gt;</c> that starts each line of a quote. Markdig does not keep their positions, so each line of the quote
    /// is read at the quote's column, allowing the three spaces of indentation Markdown does; a lazy continuation line has
    /// none.
    /// </summary>
    /// <remarks>
    /// NOTE: each quote looks only at its own lines. Quotes nest, so a scan to the end of the line from every level would
    /// take time in proportion to the square of the nesting on one line.
    /// </remarks>
    private void QuoteMarkers(QuoteBlock quote)
    {
        var range = Range(quote);
        for (var line = LineOf(range.Start); line < _lineStarts.Count && _lineStarts[line] < range.End; line++)
        {
            var marker = _lineStarts[line] + quote.Column;
            var end = Math.Min(LineEndOf(line), marker + 4);
            for (var index = marker; index < end; index++)
            {
                if (_markdown[index] == '>')
                {
                    _painter.Paint(new SourceRange(index, 1), MarkdownSyntaxKind.Marker);
                    break;
                }
                if (_markdown[index] != ' ')
                {
                    break;
                }
            }
        }
    }

    /// <summary>The <c>[!NOTE]</c> of an alert, on its first line.</summary>
    private void AlertMarker(AlertBlock alert)
    {
        var range = Range(alert);
        var open = _markdown.IndexOf("[!", range.Start, StringComparison.Ordinal);
        if (open >= 0 && open < LineEnd(range.Start))
        {
            var close = _markdown.IndexOf(']', open);
            if (close >= 0 && close < LineEnd(range.Start))
            {
                _painter.Paint(new SourceRange(open, close - open + 1), MarkdownSyntaxKind.Marker);
            }
        }
    }

    /// <summary>The bullet (<c>-</c>, <c>*</c>, <c>+</c>) or number (<c>1.</c>, <c>1)</c>) that starts a list item.</summary>
    private void ListMarker(ListItemBlock item)
    {
        var start = Range(item).Start;
        var end = start;
        while (end < _markdown.Length && char.IsAsciiDigit(_markdown[end]))
        {
            end++;
        }
        if (end < _markdown.Length && (end > start ? _markdown[end] is '.' or ')' : _markdown[end] is '-' or '*' or '+'))
        {
            _painter.Paint(new SourceRange(start, end - start + 1), MarkdownSyntaxKind.Marker);
        }
    }

    /// <summary>A table's pipes and delimiter row are markers; the cells are text.</summary>
    private void Table(Table table)
    {
        _painter.Paint(Range(table), MarkdownSyntaxKind.Marker);
        foreach (var cell in table.OfType<TableRow>().SelectMany(row => row.OfType<TableCell>()))
        {
            _painter.Paint(Range(cell), MarkdownSyntaxKind.None);
            Blocks(cell);
        }
    }

    private void LinkReferenceDefinition(LinkReferenceDefinition definition)
    {
        var range = Range(definition);
        _painter.Paint(range, MarkdownSyntaxKind.Marker);
        _painter.Paint(Part(definition.LabelSpan, range), MarkdownSyntaxKind.Link);
        _painter.Paint(Part(definition.UrlSpan, range), MarkdownSyntaxKind.Url);
        _painter.Paint(Part(definition.TitleSpan, range), MarkdownSyntaxKind.Url);
    }

    /// <summary>Queues the inlines of <paramref name="container"/>.</summary>
    private void Inlines(ContainerInline? container)
    {
        if (container is null)
        {
            return;
        }
        foreach (var inline in container)
        {
            _pending.Push(inline);
        }
    }

    private void Inline(Inline inline)
    {
        switch (inline)
        {
            case EmphasisInline emphasis:
                Emphasis(emphasis);
                break;
            case CodeInline code:
                Delimited(Range(code), code.DelimiterCount, MarkdownSyntaxKind.Code);
                break;
            case MathInline math:
                Delimited(Range(math), math.DelimiterCount, MarkdownSyntaxKind.Math);
                break;
            case AutolinkInline autolink:
                _painter.Paint(Range(autolink), MarkdownSyntaxKind.Marker);
                Inner(Range(autolink), 1, MarkdownSyntaxKind.Url);
                break;
            case LinkInline { IsAutoLink: true } bare:
                _painter.Paint(Range(bare), MarkdownSyntaxKind.Url);
                break;
            case LinkInline link:
                Link(link);
                break;
            case FootnoteLink { IsBackLink: false } reference:
                _painter.Paint(Range(reference), MarkdownSyntaxKind.Link);
                break;
            case TaskList task:
                _painter.Paint(Range(task), MarkdownSyntaxKind.Marker);
                break;
            case HtmlInline html:
                Html(Range(html));
                break;
            case ContainerInline container:
                Inlines(container);
                break;
        }
    }

    private void Emphasis(EmphasisInline emphasis)
    {
        var range = Range(emphasis);
        var style = emphasis switch
        {
            { DelimiterChar: '~' } => MarkdownTextStyles.Strikethrough,
            { DelimiterCount: >= 2 } => MarkdownTextStyles.Bold,
            _ => MarkdownTextStyles.Italic,
        };
        _painter.AddStyle(range, style);
        Markers(range, emphasis.DelimiterCount);
        Inlines(emphasis);
    }

    private void Link(LinkInline link)
    {
        // `[text](url "title")`, `![alt](src)` or `[text][label]`: the brackets and parentheses are markers, the text is
        // the link's, the destination, title and label are its address. Markdig's label span is the text itself unless
        // the link names a label of its own after the text.
        var range = Range(link);
        _painter.Paint(range, MarkdownSyntaxKind.Marker);
        var textEnd = range.Start;
        if (link.FirstChild is { } first && link.LastChild is { } last)
        {
            var start = Range(first).Start;
            textEnd = Range(last).End;
            _painter.Paint(new SourceRange(start, textEnd - start), MarkdownSyntaxKind.Link);
        }
        _painter.Paint(Part(link.UrlSpan, range), MarkdownSyntaxKind.Url);
        _painter.Paint(Part(link.TitleSpan, range), MarkdownSyntaxKind.Url);
        if (Part(link.LabelSpan, range) is { Length: > 0 } label && label.Start > textEnd)
        {
            _painter.Paint(label, MarkdownSyntaxKind.Url);
        }
        Inlines(link);
    }

    /// <summary>Raw HTML, highlighted as HTML.</summary>
    private void Html(SourceRange range)
    {
        _painter.Paint(range, MarkdownSyntaxKind.Html);
        foreach (var token in CodeHighlighter.Tokenize("html", _markdown.Substring(range.Start, range.Length)))
        {
            _painter.Paint(new SourceRange(range.Start + token.Start, token.Length), Kind(token.Kind));
        }
    }

    /// <summary>A construct with <paramref name="count"/> delimiters on each side and <paramref name="kind"/> inside.</summary>
    private void Delimited(SourceRange range, int count, MarkdownSyntaxKind kind)
    {
        Markers(range, count);
        Inner(range, count, kind);
    }

    private void Markers(SourceRange range, int count)
    {
        count = Math.Min(count, range.Length / 2);
        _painter.Paint(new SourceRange(range.Start, count), MarkdownSyntaxKind.Marker);
        _painter.Paint(new SourceRange(range.End - count, count), MarkdownSyntaxKind.Marker);
    }

    private void Inner(SourceRange range, int count, MarkdownSyntaxKind kind)
    {
        count = Math.Min(count, range.Length / 2);
        _painter.Paint(new SourceRange(range.Start + count, range.Length - 2 * count), kind);
    }

    /// <summary>
    /// A footnote's label (<c>^1</c>) with the bracket before it and the characters after it up to the colon:
    /// <c>[^1]:</c>.
    /// </summary>
    private SourceRange FootnoteLabel(SourceSpan span)
    {
        var range = Range(span);
        var start = range.Start > 0 && _markdown[range.Start - 1] == '[' ? range.Start - 1 : range.Start;
        var end = _markdown.IndexOf(':', range.End);
        return new SourceRange(start, (end >= 0 && end < LineEnd(range.End) ? end + 1 : range.End) - start);
    }

    /// <summary>
    /// A part of a node, such as a link's destination, or an empty range when the node has no such part. Markdig leaves a
    /// missing part's span at its default, the first character of the source, which is never inside the node past its
    /// opening bracket.
    /// </summary>
    private SourceRange Part(SourceSpan span, SourceRange owner)
    {
        var range = Range(span);
        return range.Start > owner.Start && range.End <= owner.End ? range : new SourceRange(owner.Start, 0);
    }

    /// <summary>The end of the line <paramref name="position"/> is on, before its line break.</summary>
    private int LineEnd(int position) => LineEndOf(LineOf(position));

    private int LineEndOf(int line)
    {
        var end = line + 1 < _lineStarts.Count ? _lineStarts[line + 1] : _markdown.Length;
        while (end > _lineStarts[line] && _markdown[end - 1] is '\n' or '\r')
        {
            end--;
        }
        return end;
    }

    private int LineOf(int position)
    {
        var line = _lineStarts.BinarySearch(position);
        return line >= 0 ? line : ~line - 1;
    }

    private SourceRange Range(MarkdownObject node) => Range(node.Span);

    private SourceRange Range(SourceSpan span)
    {
        if (span.IsEmpty || span.Start < 0 || span.Start >= _markdown.Length)
        {
            return new SourceRange(Math.Clamp(span.Start, 0, _markdown.Length), 0);
        }
        var end = Math.Min(span.End + 1, _markdown.Length);
        return new SourceRange(span.Start, end - span.Start);
    }
}
