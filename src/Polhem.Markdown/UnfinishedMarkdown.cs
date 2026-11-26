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
        var index = 0;
        while (index < block.Length)
        {
            var step = block[index] switch
            {
                '\\' => new Step(index + 2),
                '`' => CodeSpanStep(block, index),
                '<' => TagStep(block, index),
                '[' => OpenBracketStep(block, index, brackets),
                ']' when brackets.Count > 0 => CloseBracketStep(block, index, brackets.Pop()),
                _ => new Step(index + 1),
            };
            if (step.Result is { } result)
            {
                return result;
            }
            index = step.Next;
        }

        // The brackets still open have not reached their `]`: an image waits, and a link shows its text so far. They are
        // taken innermost first, so the positions of those around them stay true, as in a badge that links somewhere.
        foreach (var (open, image) in brackets)
        {
            block = image ? block[..open].TrimEnd() : block.Remove(open, 1);
        }
        return block;
    }

    /// <summary>Where the scan of a block goes on from, or the completed block when the scan ends there.</summary>
    private readonly record struct Step(int Next, string? Result = null);

    /// <summary>Skips a code span, or closes one that is open, holding back a lone backtick at the end.</summary>
    private static Step CodeSpanStep(string block, int index)
    {
        var run = RunLength(block, index, '`');
        var close = FindRun(block, index + run, '`', run);
        if (close >= 0)
        {
            return new Step(close + run);
        }
        return new Step(block.Length, block[(index + run)..].Trim().Length == 0
            ? block[..index].TrimEnd()
            : block + new string('`', run));
    }

    /// <summary>Holds back a tag or an autolink still being typed, or a lone <c>&lt;</c> at the end.</summary>
    private static Step TagStep(string block, int index)
    {
        var typing = index + 1 == block.Length
            || ((char.IsAsciiLetter(block[index + 1]) || block[index + 1] == '/') && block.IndexOf('>', index) < 0);
        return typing ? new Step(block.Length, block[..index].TrimEnd()) : new Step(index + 1);
    }

    private static Step OpenBracketStep(string block, int index, Stack<(int Position, bool Image)> brackets)
    {
        var image = index > 0 && block[index - 1] == '!';
        brackets.Push((image ? index - 1 : index, image));
        return new Step(index + 1);
    }

    /// <summary>
    /// Skips a link or image whose address has ended. One whose address has not shows as its text, or waits if it is an
    /// image; <c>[text]</c> at the very end, most likely a link whose address comes next, shows as its text too.
    /// </summary>
    private static Step CloseBracketStep(string block, int index, (int Position, bool Image) bracket)
    {
        var (open, image) = bracket;
        if (index + 1 < block.Length && block[index + 1] == '(')
        {
            var end = FindDestinationEnd(block, index + 2);
            if (end >= 0)
            {
                return new Step(end + 1);
            }
            return new Step(block.Length, image ? block[..open].TrimEnd() : block[..open] + block[(open + 1)..index]);
        }
        if (index + 1 == block.Length && !image && !IsTaskMarker(block, open))
        {
            return new Step(block.Length, block[..open] + block[(open + 1)..index]);
        }
        return new Step(index + 1);
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
        var index = start;
        while (index < block.Length)
        {
            var c = block[index];
            if (c == '\n' || (c == ')' && depth == 0))
            {
                return c == ')' ? index : -1;
            }
            depth += c switch
            {
                '(' => 1,
                ')' => -1,
                _ => 0,
            };
            index += c == '\\' ? 2 : 1;
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
        var index = text.IndexOf(c, start);
        while (index >= 0)
        {
            var run = RunLength(text, index, c);
            if (run == length)
            {
                return index;
            }
            index = text.IndexOf(c, index + run);
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
        var index = 0;
        while (index < block.Length)
        {
            var c = block[index];
            if (c is '*' or '_' or '~')
            {
                if (IsPendingMarker(block, index))
                {
                    // A marker at the very end with nothing after it yet: it opens what has not arrived.
                    return CloseEmphasis(block[..index].TrimEnd());
                }
                index = EmphasisRun(block, index, open);
            }
            else
            {
                index = c switch
                {
                    '\\' => index + 2,
                    '`' => SkipCodeSpan(block, index),
                    ']' => SkipDestination(block, index),
                    _ => index + 1,
                };
            }
        }
        if (open.Count == 0)
        {
            return block;
        }
        var closed = new StringBuilder(block.TrimEnd());
        for (var entry = open.Count - 1; entry >= 0; entry--)
        {
            closed.Append(open[entry].Marker, open[entry].Length);
        }
        return closed.ToString();
    }

    /// <summary>Where a code span at <paramref name="index"/> ends; an open one holds no emphasis to the end.</summary>
    private static int SkipCodeSpan(string block, int index)
    {
        var run = RunLength(block, index, '`');
        var close = FindRun(block, index + run, '`', run);
        return close < 0 ? block.Length : close + run;
    }

    /// <summary>Where the destination after a link's <c>]</c> ends, since a destination holds no emphasis.</summary>
    private static int SkipDestination(string block, int index)
    {
        if (index + 1 < block.Length && block[index + 1] == '(' && FindDestinationEnd(block, index + 2) is >= 0 and var end)
        {
            return end + 1;
        }
        return index + 1;
    }

    /// <summary>Whether the marker run at <paramref name="index"/> ends the block after a space, with nothing after it.</summary>
    private static bool IsPendingMarker(string block, int index)
    {
        var length = RunLength(block, index, block[index]);
        return index + length == block.Length && (index == 0 || char.IsWhiteSpace(block[index - 1]))
            && (block[index] != '~' || length == 2) && !IsThematicBreakLine(block, index);
    }

    /// <summary>
    /// Takes the marker run at <paramref name="index"/>: it closes the innermost open run it matches, or opens one, and
    /// returns where the scan goes on from. A rule, an intraword underscore or a tilde that is no strikethrough is text.
    /// </summary>
    private static int EmphasisRun(string block, int index, List<(char Marker, int Length)> open)
    {
        var c = block[index];
        var length = RunLength(block, index, c);
        if (IsThematicBreakLine(block, index))
        {
            return LineEnd(block, index);
        }
        var before = index > 0 ? block[index - 1] : '\n';
        var after = index + length < block.Length ? block[index + length] : '\n';
        var text = (c == '~' && length != 2) || (c == '_' && char.IsLetterOrDigit(before) && char.IsLetterOrDigit(after));
        if (text)
        {
            return index + length;
        }
        var match = char.IsWhiteSpace(before) ? -1 : open.FindLastIndex(entry => entry.Marker == c && entry.Length == length);
        if (match >= 0)
        {
            open.RemoveRange(match, open.Count - match);
        }
        else if (!char.IsWhiteSpace(after))
        {
            open.Add((c, length));
        }
        return index + length;
    }
}
