using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Polhem.Markdown.Editing;

/// <summary>
/// The edits a Markdown editor makes for its commands, worked out on the text alone so any editor can apply them:
/// formats (<see cref="Apply"/>), the next heading level (<see cref="CycleHeading"/>), continuing a list on Enter
/// (<see cref="ContinueList"/>) and moving a list item in or out (<see cref="IndentListItem"/>).
/// </summary>
/// <remarks>
/// Each command is one <see cref="MarkdownEdit"/>, so an editor applies it as one step to undo. Offsets are UTF-16 code
/// units, as in the text, and lines may end in <c>\n</c> or <c>\r\n</c>; a line keeps its own ending.
/// </remarks>
internal static partial class MarkdownFormatter
{
    /// <summary>
    /// Applies <paramref name="format"/> to the selection, or removes it when the selection already has it.
    /// </summary>
    /// <remarks>
    /// An inline format wraps the selection in its markers, or unwraps it when the markers are just inside or just
    /// outside the selection; with nothing selected it inserts a pair with the caret between, or removes an empty pair
    /// around the caret. A line format applies to every line the selection touches, and is removed when every one of
    /// those lines has it. <see cref="MarkdownFormat.Table"/> and <see cref="MarkdownFormat.HorizontalRule"/> insert a
    /// block instead, and are never removed.
    /// </remarks>
    public static MarkdownEdit Apply(string text, int selectionStart, int selectionLength, MarkdownFormat format)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var end = Math.Clamp(selectionStart + selectionLength, start, text.Length);
        return format switch
        {
            MarkdownFormat.Bold => Toggle(text, start, end, '*', 2),
            MarkdownFormat.Italic => Toggle(text, start, end, '*', 1),
            MarkdownFormat.Strikethrough => Toggle(text, start, end, '~', 2),
            MarkdownFormat.InlineCode => Toggle(text, start, end, '`', 1),
            MarkdownFormat.Link => Link(text, start, end, "["),
            MarkdownFormat.Image => Link(text, start, end, "!["),
            MarkdownFormat.Heading1 => Lines(text, start, end, lines => Heading(lines, 1)),
            MarkdownFormat.Heading2 => Lines(text, start, end, lines => Heading(lines, 2)),
            MarkdownFormat.Heading3 => Lines(text, start, end, lines => Heading(lines, 3)),
            MarkdownFormat.BulletList => Lines(text, start, end, Bullets),
            MarkdownFormat.NumberedList => Lines(text, start, end, Numbers),
            MarkdownFormat.TaskList => Lines(text, start, end, Tasks),
            MarkdownFormat.Quote => Lines(text, start, end, Quotes),
            MarkdownFormat.CodeBlock => CodeBlock(text, start, end),
            MarkdownFormat.Table => Table(text, start, end),
            MarkdownFormat.HorizontalRule => HorizontalRule(text, start, end),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null),
        };
    }

    /// <summary>
    /// Makes the lines the selection touches headings one level deeper than the first of them is, from plain text to
    /// level 1, then 2, then 3, and back to plain text from level 3 or deeper.
    /// </summary>
    /// <remarks>Lines without text are left as they are.</remarks>
    public static MarkdownEdit CycleHeading(string text, int selectionStart, int selectionLength)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var end = Math.Clamp(selectionStart + selectionLength, start, text.Length);
        return Lines(text, start, end, lines =>
        {
            var first = lines.FirstOrDefault(IsText);
            var level = first is not null && HeadingRegex().Match(first) is { Success: true } heading ? heading.Groups["hashes"].Length : 0;
            return level >= 3 ? PlainText(lines) : Heading(lines, level + 1);
        });
    }

    /// <summary>
    /// The edit for Enter at <paramref name="caret"/> on a list item: a new item below with the next bullet or number, or,
    /// on an item with no text, the end of the list. <see langword="null"/> when the caret is not past the marker of a
    /// list item, so Enter does what it always does.
    /// </summary>
    public static MarkdownEdit? ContinueList(string text, int caret)
    {
        ArgumentNullException.ThrowIfNull(text);
        caret = Math.Clamp(caret, 0, text.Length);
        var line = LineAt(text, caret);
        var item = ListItemRegex().Match(line.Text);
        if (!item.Success || caret < line.Start + item.Length)
        {
            return null;
        }
        if (line.Text[item.Length..].Trim().Length == 0)
        {
            return new MarkdownEdit(line.Start, line.Text.Length, "", line.Start, 0);
        }

        var next = new StringBuilder(LineBreak(text, line));
        next.Append(item.Groups["indent"].Value);
        if (item.Groups["number"].Success)
        {
            var number = long.Parse(item.Groups["number"].Value, CultureInfo.InvariantCulture) + 1;
            next.Append(number.ToString(CultureInfo.InvariantCulture)).Append(item.Groups["delimiter"].Value);
        }
        else
        {
            next.Append(item.Groups["bullet"].Value);
        }
        next.Append(item.Groups["space"].Value.Length > 0 ? item.Groups["space"].Value : " ");
        if (item.Groups["task"].Success)
        {
            next.Append("[ ] ");
        }
        return new MarkdownEdit(caret, 0, next.ToString(), caret + next.Length, 0);
    }

    /// <summary>
    /// The edit for Tab (<paramref name="outdent"/> <see langword="false"/>) or Shift+Tab on a list item: the item moves
    /// under the content of the item above it, or back to the level of its parent. <see langword="null"/> when the
    /// caret's line is not a list item, or the selection spans lines, so the keys do what they always do.
    /// </summary>
    public static MarkdownEdit? IndentListItem(string text, int selectionStart, int selectionLength, bool outdent)
    {
        ArgumentNullException.ThrowIfNull(text);
        var start = Math.Clamp(selectionStart, 0, text.Length);
        var end = Math.Clamp(selectionStart + selectionLength, start, text.Length);
        var line = LineAt(text, start);
        var item = ListItemRegex().Match(line.Text);
        if (!item.Success || end > line.Start + line.Text.Length)
        {
            return null;
        }

        var indent = item.Groups["indent"].Length;
        int target;
        if (outdent)
        {
            if (indent == 0)
            {
                return null;
            }
            target = PreviousItems(text, line.Start).FirstOrDefault(other => other.Indent < indent).Indent;
        }
        else
        {
            var above = PreviousItems(text, line.Start).FirstOrDefault(other => other.Indent <= indent);
            target = above.ContentColumn > indent ? above.ContentColumn : item.Length;
        }

        var shift = target - indent;
        var edit = shift > 0
            ? new MarkdownEdit(line.Start, 0, new string(' ', shift), 0, 0)
            : new MarkdownEdit(line.Start, -shift, "", 0, 0);
        return edit with
        {
            SelectionStart = Math.Max(line.Start, start + shift),
            SelectionLength = end - start,
        };
    }

    /// <summary>
    /// Wraps the selection in a marker of <paramref name="width"/> <paramref name="marker"/> characters, or removes it.
    /// </summary>
    private static MarkdownEdit Toggle(string text, int start, int end, char marker, int width)
    {
        // The markers just inside the selection.
        var inside = Run(text, start, end, marker, forward: true);
        var insideEnd = Run(text, start, end, marker, forward: false);
        if (end - start >= 2 * width && inside + insideEnd <= end - start && Has(marker, width, inside, insideEnd))
        {
            var inner = text[(start + width)..(end - width)];
            return new MarkdownEdit(start, end - start, inner, start, inner.Length);
        }

        // The markers just outside it.
        var before = Run(text, 0, start, marker, forward: false);
        var after = Run(text, end, text.Length, marker, forward: true);
        if (Has(marker, width, before, after))
        {
            return new MarkdownEdit(start - width, end - start + 2 * width, text[start..end], start - width, end - start);
        }

        // Emphasis cannot open before a space or close after one, so the markers go around the selected words.
        while (start < end && char.IsWhiteSpace(text[start]))
        {
            start++;
        }
        while (end > start && char.IsWhiteSpace(text[end - 1]))
        {
            end--;
        }
        var markers = new string(marker, width);
        return new MarkdownEdit(start, end - start, markers + text[start..end] + markers, start + width, end - start);
    }

    /// <summary>
    /// Whether runs of <paramref name="opening"/> and <paramref name="closing"/> markers hold one of
    /// <paramref name="width"/>. Asterisks pair up: one is italic, two bold, three both, so italic needs an odd run.
    /// </summary>
    private static bool Has(char marker, int width, int opening, int closing)
    {
        var shared = Math.Min(opening, closing);
        return shared >= width && (marker != '*' || width != 1 || shared % 2 == 1);
    }

    /// <summary>How many <paramref name="marker"/> characters run from one end of <c>[from, to)</c>.</summary>
    private static int Run(string text, int from, int to, char marker, bool forward)
    {
        var count = 0;
        if (forward)
        {
            while (from + count < to && text[from + count] == marker)
            {
                count++;
            }
        }
        else
        {
            while (to - count > from && text[to - count - 1] == marker)
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>A link, or with <paramref name="opening"/> <c>![</c> an image, around the selection as its text.</summary>
    private static MarkdownEdit Link(string text, int start, int end, string opening)
    {
        const string Address = "url";
        var label = text[start..end];
        var link = opening + label + "](" + Address + ")";
        return label.Length == 0
            ? new MarkdownEdit(start, 0, link, start + opening.Length, 0)
            : new MarkdownEdit(start, end - start, link, start + opening.Length + label.Length + 2, Address.Length);
    }

    private static MarkdownEdit CodeBlock(string text, int start, int end)
    {
        var region = Region(text, start, end);
        var lines = region.Lines;
        var fenced = lines.Count >= 2
            && lines[0].Text.TrimStart().StartsWith("```", StringComparison.Ordinal)
            && lines[^1].Text.Trim() == "```";
        if (fenced)
        {
            var inner = Join(lines.Skip(1).Take(lines.Count - 2).ToList());
            return new MarkdownEdit(region.Start, region.Length, inner, region.Start, inner.Length);
        }

        var lineBreak = LineBreak(text, LineAt(text, region.Start));
        var content = Join(lines);
        var block = "```" + lineBreak + content + lineBreak + "```";
        return new MarkdownEdit(region.Start, region.Length, block, region.Start + 3 + lineBreak.Length, content.Length);
    }

    /// <summary>Replaces the lines the selection touches with <paramref name="transform"/> of their text.</summary>
    private static MarkdownEdit Lines(string text, int start, int end, Func<IReadOnlyList<string>, List<string>> transform)
    {
        var region = Region(text, start, end);
        var replaced = transform(region.Lines.Select(line => line.Text).ToList());
        var joined = Join(region.Lines.Select((line, index) => line with { Text = replaced[index] }).ToList());
        if (start == end && region.Lines.Count == 1)
        {
            // A caret stays with the text it was in, before or after the marker that changed.
            var caret = Math.Clamp(start + joined.Length - region.Length, region.Start, region.Start + joined.Length);
            return new MarkdownEdit(region.Start, region.Length, joined, caret, 0);
        }
        return new MarkdownEdit(region.Start, region.Length, joined, region.Start, joined.Length);
    }

    private static List<string> Heading(IReadOnlyList<string> lines, int level)
    {
        var all = lines.Where(IsText).All(line => HeadingRegex().Match(line) is { Success: true } heading && heading.Groups["hashes"].Length == level);
        return [.. lines.Select(line =>
        {
            if (!IsText(line))
            {
                return line;
            }
            var heading = HeadingRegex().Match(line);
            var rest = heading.Success ? line[heading.Length..] : line.TrimStart();
            return all ? rest : new string('#', level) + " " + rest;
        })];
    }

    /// <summary>The lines with any heading marker taken off.</summary>
    private static List<string> PlainText(IReadOnlyList<string> lines) =>
        [.. lines.Select(line => HeadingRegex().Match(line) is { Success: true } heading ? line[heading.Length..] : line)];

    private static List<string> Bullets(IReadOnlyList<string> lines) =>
        Items(lines, item => item.Groups["bullet"].Success && !item.Groups["task"].Success, (_, indent, content) => indent + "- " + content);

    private static List<string> Numbers(IReadOnlyList<string> lines) =>
        Items(lines, item => item.Groups["number"].Success, (number, indent, content) => indent + number.ToString(CultureInfo.InvariantCulture) + ". " + content);

    private static List<string> Tasks(IReadOnlyList<string> lines) =>
        Items(lines, item => item.Groups["task"].Success, (_, indent, content) => indent + "- [ ] " + content);

    /// <summary>
    /// Makes every line with text a list item of one kind, replacing any other list marker it had, or makes them plain
    /// text when all of them already are that kind.
    /// </summary>
    private static List<string> Items(IReadOnlyList<string> lines, Func<Match, bool> isKind, Func<int, string, string, string> item)
    {
        var all = lines.Where(IsText).All(line => ListItemRegex().Match(line) is { Success: true } match && isKind(match));
        var number = 0;
        return [.. lines.Select(line =>
        {
            if (!IsText(line))
            {
                return line;
            }
            var match = ListItemRegex().Match(line);
            var indent = match.Success ? match.Groups["indent"].Value : line[..(line.Length - line.TrimStart().Length)];
            var content = match.Success ? line[match.Length..] : line.TrimStart();
            return all ? indent + content : item(++number, indent, content);
        })];
    }

    private static List<string> Quotes(IReadOnlyList<string> lines)
    {
        var all = lines.Where(IsText).All(line => QuoteRegex().IsMatch(line));
        return [.. lines.Select(line =>
        {
            if (all)
            {
                var quote = QuoteRegex().Match(line);
                return quote.Success ? quote.Groups["indent"].Value + line[quote.Length..] : line;
            }
            return line.Length == 0 ? ">" : "> " + line;
        })];
    }

    private static bool IsText(string line) => line.Trim().Length > 0;

    /// <summary>The list items above <paramref name="lineStart"/>, nearest first, up to the first line that is not one.</summary>
    private static IEnumerable<(int Indent, int ContentColumn)> PreviousItems(string text, int lineStart)
    {
        while (lineStart > 0)
        {
            var line = LineAt(text, lineStart - 1);
            lineStart = line.Start;
            if (!IsText(line.Text))
            {
                continue;
            }
            var item = ListItemRegex().Match(line.Text);
            if (!item.Success)
            {
                // A continuation line of an item belongs to it; anything at the margin ends the list.
                if (line.Text.Length - line.Text.TrimStart().Length == 0)
                {
                    yield break;
                }
                continue;
            }
            yield return (item.Groups["indent"].Length, item.Length);
        }
    }

    /// <summary>The whole lines from the one holding <paramref name="start"/> to the one holding the end of the selection.</summary>
    private static (int Start, int Length, List<Line> Lines) Region(string text, int start, int end)
    {
        // A selection that ends at the start of a line does not touch that line.
        if (end > start && end > 0 && text[end - 1] == '\n')
        {
            end--;
        }
        var first = LineAt(text, start);
        var lines = new List<Line> { first };
        while (lines[^1].Start + lines[^1].Text.Length < end && lines[^1].Break.Length > 0)
        {
            lines.Add(LineAt(text, lines[^1].Start + lines[^1].Text.Length + lines[^1].Break.Length));
        }
        var last = lines[^1];
        var length = last.Start + last.Text.Length - first.Start;
        return (first.Start, length, [.. lines.Select((line, index) => index == lines.Count - 1 ? line with { Break = "" } : line)]);
    }

    /// <summary>The lines joined by their own line breaks; the last line's break is not part of a region.</summary>
    private static string Join(IReadOnlyList<Line> lines)
    {
        var text = new StringBuilder();
        for (var index = 0; index < lines.Count; index++)
        {
            text.Append(lines[index].Text);
            if (index < lines.Count - 1)
            {
                text.Append(lines[index].Break.Length > 0 ? lines[index].Break : "\n");
            }
        }
        return text.ToString();
    }

    /// <summary>The break that ends <paramref name="line"/>, or for the last line, the break the text uses.</summary>
    private static string LineBreak(string text, Line line) =>
        line.Break.Length > 0 ? line.Break : text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";

    /// <summary>The line that holds <paramref name="offset"/>: where it starts, its text, and the break that ends it.</summary>
    private static Line LineAt(string text, int offset)
    {
        var start = offset == 0 ? 0 : text.LastIndexOf('\n', offset - 1) + 1;
        var newline = text.IndexOf('\n', start);
        var end = newline < 0 ? text.Length : newline;
        var lineEnd = end > start && text[end - 1] == '\r' ? end - 1 : end;
        var lineBreak = newline < 0 ? "" : text[lineEnd..(newline + 1)];
        return new Line(start, text[start..lineEnd], lineBreak);
    }

    /// <summary>A bullet (<c>-</c>, <c>*</c>, <c>+</c>) or number (<c>1.</c>, <c>1)</c>), the space after it, and a task box.</summary>
    [GeneratedRegex(@"^(?<indent>[ \t]*)(?:(?<bullet>[-*+])|(?<number>\d{1,9})(?<delimiter>[.)]))(?<space> +|$)(?<task>\[[ xX]\] +)?")]
    private static partial Regex ListItemRegex();

    [GeneratedRegex(@"^ {0,3}(?<hashes>#{1,6})(?: +|$)")]
    private static partial Regex HeadingRegex();

    [GeneratedRegex(@"^(?<indent>[ \t]*)> ?")]
    private static partial Regex QuoteRegex();

    private sealed record Line(int Start, string Text, string Break);
}
