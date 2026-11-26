using System.Text;

namespace Polhem.Markdown;

/// <summary>
/// Closes the syntax a reply cut off while it is still arriving leaves open, so the text shows as it will once the
/// reply is complete instead of showing its markers and then jumping (polhem-dev/polhem-markdown#69).
/// </summary>
/// <remarks>
/// Only the last block changes: everything before its start is returned as it is, so the source offsets of the
/// blocks before it stay true. Within it:
/// <list type="bullet">
/// <item>an open code fence or math block is left alone, since the rest of the reply is its content either way;</item>
/// <item>a table's header row gets a delimiter row, so it shows as a table and not as a line of pipes;</item>
/// <item>an HTML tag or autolink still being typed is held back;</item>
/// <item>an image whose address has not ended is held back, and a link's text shows as plain text until its address
/// ends;</item>
/// <item>an open code span is closed, and a lone backtick at the end held back;</item>
/// <item>open emphasis, strong emphasis and strikethrough are closed, and a marker with nothing after it yet held
/// back.</item>
/// </list>
/// A complete text comes back unchanged.
/// </remarks>
internal static class UnfinishedMarkdown
{
    public static string Complete(string markdown)
    {
        if (markdown.Length == 0 || LastBlockStart(markdown) is not { } start)
        {
            return markdown;
        }
        var block = markdown[start..];
        var completed = CompleteBlock(block);
        return string.Equals(completed, block, StringComparison.Ordinal) ? markdown : markdown[..start] + completed;
    }

    /// <summary>
    /// Where the last block starts: after the last blank line outside a code fence or a math block. Returns
    /// <see langword="null"/> when the text ends inside a fence, which needs nothing closed.
    /// </summary>
    private static int? LastBlockStart(string markdown)
    {
        var start = 0;
        string? fence = null;
        var lineStart = 0;
        while (lineStart <= markdown.Length)
        {
            var lineEnd = markdown.IndexOf('\n', lineStart);
            if (lineEnd < 0)
            {
                lineEnd = markdown.Length;
            }
            var line = markdown.AsSpan(lineStart, lineEnd - lineStart).TrimEnd('\r');
            var trimmed = line.TrimStart(' ');
            if (fence is null)
            {
                if (line.Length - trimmed.Length <= 3 && FenceOf(trimmed) is { } opened)
                {
                    fence = opened;
                }
                else if (trimmed.IsWhiteSpace() && lineEnd < markdown.Length)
                {
                    start = lineEnd + 1;
                }
            }
            else if (trimmed.StartsWith(fence, StringComparison.Ordinal) && trimmed.TrimEnd().Length >= fence.Length
                && trimmed.TrimEnd().ToString().All(c => c == fence[0]))
            {
                fence = null;
                start = lineEnd + 1;
            }
            lineStart = lineEnd + 1;
        }
        return fence is null ? Math.Min(start, markdown.Length) : null;
    }

    /// <summary>The fence a line opens: a run of three or more backticks or tildes, or <c>$$</c>.</summary>
    private static string? FenceOf(ReadOnlySpan<char> line)
    {
        if (line.StartsWith("$$", StringComparison.Ordinal))
        {
            return "$$";
        }
        if (line.Length < 3 || (line[0] != '`' && line[0] != '~'))
        {
            return null;
        }
        var length = 0;
        while (length < line.Length && line[length] == line[0])
        {
            length++;
        }
        return length >= 3 ? new string(line[0], length) : null;
    }

    private static string CompleteBlock(string block)
    {
        var text = CompleteStructure(block);

        // NOTE: emphasis in a table closes cell by cell, so closing it at the end of a row would be wrong; a cell's
        // markers show until its emphasis ends.
        if (!text.TrimStart().StartsWith('|'))
        {
            text = CloseEmphasis(text);
        }
        return CompleteTable(text);
    }

    /// <summary>
    /// Gives a table that has only its header row a delimiter row, or completes one that is still arriving, so the row
    /// shows as the header of a table.
    /// </summary>
    private static string CompleteTable(string block)
    {
        var lines = block.Split('\n');
        var header = lines[0].TrimEnd('\r');
        if (!header.TrimStart().StartsWith('|') || CountCells(header) == 0)
        {
            return block;
        }
        var delimiter = "|" + string.Concat(Enumerable.Repeat(" --- |", CountCells(header)));
        if (lines.Length == 1)
        {
            return block + "\n" + delimiter;
        }
        if (lines.Length == 2 && IsPartialDelimiter(lines[1]))
        {
            return lines[0] + "\n" + delimiter;
        }
        return block;
    }

    private static int CountCells(string row)
    {
        var cells = row.Trim().Trim('|').Split('|');
        return cells.Length == 1 && cells[0].Trim().Length == 0 ? 0 : cells.Length;
    }

    private static bool IsPartialDelimiter(string line) =>
        line.Trim().Length == 0 || line.Trim().All(c => c is '|' or '-' or ':' or ' ');

    /// <summary>
    /// Holds back a tag or autolink still being typed, an image whose address has not ended and a lone backtick, shows
    /// an unfinished link as its text, and closes an open code span.
    /// </summary>
    private static string CompleteStructure(string block)
    {
        var brackets = new Stack<(int Position, bool Image)>();
        for (var index = 0; index < block.Length; index++)
        {
            var c = block[index];
            if (c == '\\')
            {
                index++;
                continue;
            }
            if (c == '`')
            {
                var run = RunLength(block, index, '`');
                var close = FindRun(block, index + run, '`', run);
                if (close >= 0)
                {
                    index = close + run - 1;
                    continue;
                }
                var rest = block[(index + run)..];
                return rest.Trim().Length == 0
                    ? block[..index].TrimEnd()
                    : block + new string('`', run);
            }
            if (c == '<' && index + 1 < block.Length && (char.IsAsciiLetter(block[index + 1]) || block[index + 1] == '/')
                && block.IndexOf('>', index) < 0)
            {
                return block[..index].TrimEnd();
            }
            if (c == '<' && index + 1 == block.Length)
            {
                return block[..index].TrimEnd();
            }
            if (c == '[')
            {
                brackets.Push((index > 0 && block[index - 1] == '!' ? index - 1 : index, index > 0 && block[index - 1] == '!'));
                continue;
            }
            if (c == ']' && brackets.Count > 0)
            {
                var (open, image) = brackets.Pop();
                if (index + 1 < block.Length && block[index + 1] == '(')
                {
                    var end = FindDestinationEnd(block, index + 2);
                    if (end >= 0)
                    {
                        index = end;
                        continue;
                    }

                    // The address has not ended: an image waits, and a link shows its text.
                    return image ? block[..open].TrimEnd() : block[..open] + block[(open + 1)..index];
                }
                if (index + 1 == block.Length && !image && !IsTaskMarker(block, open))
                {
                    // `[text]` at the very end is most likely a link whose address comes next.
                    return block[..open] + block[(open + 1)..index];
                }
            }
        }
        // The brackets still open have not reached their `]`: an image waits, and a link shows its text so far. They are
        // taken innermost first, so the positions of those around them stay true, as in a badge that links somewhere.
        foreach (var (open, image) in brackets)
        {
            block = image ? block[..open].TrimEnd() : block.Remove(open, 1);
        }
        return block;
    }

    /// <summary>Whether the bracket at <paramref name="open"/> is a task list item's box, <c>[ ]</c> or <c>[x]</c>.</summary>
    private static bool IsTaskMarker(string block, int open)
    {
        var lineStart = block.LastIndexOf('\n', Math.Max(0, open - 1)) + 1;
        var before = block.AsSpan(lineStart, open - lineStart).Trim();
        return before.Length is 1 or 2 && (before[^1] is '-' or '*' or '+' or '.' or ')');
    }

    /// <summary>The index of the <c>)</c> that ends a link destination starting at <paramref name="start"/>, or -1.</summary>
    private static int FindDestinationEnd(string block, int start)
    {
        var depth = 0;
        for (var index = start; index < block.Length; index++)
        {
            switch (block[index])
            {
                case '\\':
                    index++;
                    break;
                case '(':
                    depth++;
                    break;
                case ')' when depth == 0:
                    return index;
                case ')':
                    depth--;
                    break;
                case '\n':
                    return -1;
            }
        }
        return -1;
    }

    /// <summary>Whether the line at <paramref name="index"/> is a rule: three or more of one marker and spaces only.</summary>
    private static bool IsThematicBreakLine(string text, int index)
    {
        var lineStart = index == 0 ? 0 : text.LastIndexOf('\n', index - 1) + 1;
        var line = text[lineStart..LineEnd(text, index)].Trim();
        return line.Length >= 3 && (line[0] is '*' or '_' or '-')
            && line.Count(c => c != ' ') >= 3 && line.All(c => c == line[0] || c == ' ');
    }

    private static int LineEnd(string text, int index)
    {
        var end = text.IndexOf('\n', index);
        return end < 0 ? text.Length : end;
    }

    private static int RunLength(string text, int start, char c)
    {
        var length = 0;
        while (start + length < text.Length && text[start + length] == c)
        {
            length++;
        }
        return length;
    }

    /// <summary>The start of the next run of exactly <paramref name="length"/> of <paramref name="c"/>, or -1.</summary>
    private static int FindRun(string text, int start, char c, int length)
    {
        for (var index = start; index < text.Length; index++)
        {
            if (text[index] != c)
            {
                continue;
            }
            var run = RunLength(text, index, c);
            if (run == length)
            {
                return index;
            }
            index += run - 1;
        }
        return -1;
    }

    /// <summary>
    /// Closes the emphasis, strong emphasis and strikethrough still open at the end of the block, innermost first, and
    /// holds back a marker that has nothing after it yet.
    /// </summary>
    private static string CloseEmphasis(string block)
    {
        var open = new List<(char Marker, int Length)>();
        for (var index = 0; index < block.Length; index++)
        {
            var c = block[index];
            if (c == '\\')
            {
                index++;
                continue;
            }
            if (c == '`')
            {
                var run = RunLength(block, index, '`');
                var close = FindRun(block, index + run, '`', run);
                if (close < 0)
                {
                    break;
                }
                index = close + run - 1;
                continue;
            }
            if (c == ']' && index + 1 < block.Length && block[index + 1] == '(')
            {
                // A link destination holds no emphasis.
                var end = FindDestinationEnd(block, index + 2);
                if (end >= 0)
                {
                    index = end;
                }
                continue;
            }
            if (c is not ('*' or '_' or '~'))
            {
                continue;
            }
            var length = RunLength(block, index, c);
            var before = index > 0 ? block[index - 1] : '\n';
            var after = index + length < block.Length ? block[index + length] : '\n';
            if (c == '~' && length != 2)
            {
                index += length - 1;
                continue;
            }
            if (IsThematicBreakLine(block, index))
            {
                index = LineEnd(block, index) - 1;
                continue;
            }
            if (index + length == block.Length && (char.IsWhiteSpace(before) || index == 0))
            {
                // A marker at the very end with nothing after it yet: it opens what has not arrived.
                return CloseEmphasis(block[..index].TrimEnd());
            }
            var leftFlanking = !char.IsWhiteSpace(after);
            var rightFlanking = !char.IsWhiteSpace(before);
            var intraword = char.IsLetterOrDigit(before) && char.IsLetterOrDigit(after);
            if (c == '_' && intraword)
            {
                index += length - 1;
                continue;
            }
            var match = rightFlanking ? open.FindLastIndex(entry => entry.Marker == c && entry.Length == length) : -1;
            if (match >= 0)
            {
                open.RemoveRange(match, open.Count - match);
            }
            else if (leftFlanking)
            {
                open.Add((c, length));
            }
            index += length - 1;
        }
        if (open.Count == 0)
        {
            return block;
        }
        var closed = new StringBuilder(block.TrimEnd());
        for (var index = open.Count - 1; index >= 0; index--)
        {
            closed.Append(open[index].Marker, open[index].Length);
        }
        return closed.ToString();
    }
}
