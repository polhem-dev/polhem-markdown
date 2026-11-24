using System.Buffers;
using Markdig.Extensions.Mathematics;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Syntax;

namespace Polhem.Markdown.Formulas;

/// <summary>
/// Markdig's inline math, refused where GitHub's rules do not read math, so dollar signs in prose stay text: a single
/// <c>$</c> followed by a space or preceded by one, as in <c>$ 5 and $ 6</c>, opens or closes nothing; and in a table,
/// where GitHub splits a row into cells before it reads math, a formula does not run across a cell's <c>|</c>.
/// </summary>
internal sealed class GitHubMathInlineParser : MathInlineParser
{
    /// <summary>The characters a delimiter row of a pipe table is made of.</summary>
    private static readonly SearchValues<char> s_delimiterRow = SearchValues.Create("-:| \t\r");

    public override bool Match(InlineProcessor processor, ref StringSlice slice)
    {
        var start = slice.Start;
        var text = slice.Text;
        if (!base.Match(processor, ref slice))
        {
            return false;
        }
        if (processor.Inline is MathInline math && IsRefused(math, processor, text, start, slice.Start))
        {
            processor.Inline = null;
            slice.Start = start;
            return false;
        }
        return true;
    }

    private bool IsRefused(MathInline math, InlineProcessor processor, string text, int start, int end)
    {
        var count = math.DelimiterCount;
        if (count == 1 && (char.IsWhiteSpace(text[start + 1]) || char.IsWhiteSpace(text[end - 2])))
        {
            return true;
        }
        return text.AsSpan(start, end - start).Contains('|')
            && processor.Block is ParagraphBlock paragraph
            && start >= TableStart(text, paragraph);
    }

    /// <summary>The last paragraph checked, so a paragraph with many formulas is scanned once.</summary>
    private TableCheck? _lastCheck;

    /// <summary>
    /// Where the pipe table in a paragraph begins, at its header line, or <see cref="int.MaxValue"/> when the paragraph
    /// holds none. A table is a delimiter row such as <c>|---|:--:|</c> and the line above it, and it may follow lines of
    /// text in the same paragraph, which are not part of it.
    /// </summary>
    /// <remarks>
    /// NOTE: the inline parsers read a paragraph of several lines from a string of its own, but a paragraph of one line
    /// from the whole document. One line is never a table, so it is told apart by its text being longer than the
    /// paragraph's own source; the document is never scanned. The answer for a paragraph is kept, since each formula
    /// with a bar asks again, which would otherwise cost the paragraph's length per formula. Parsers on several threads
    /// share this one, so the answer is kept in one immutable object; a race only scans again.
    /// </remarks>
    private int TableStart(string text, ParagraphBlock paragraph)
    {
        if (text.Length > paragraph.Span.Length)
        {
            return int.MaxValue;
        }
        var last = Volatile.Read(ref _lastCheck);
        if (last is not null && ReferenceEquals(last.Text, text))
        {
            return last.TableStart;
        }
        var tableStart = FindTableStart(text);
        Volatile.Write(ref _lastCheck, new TableCheck(text, tableStart));
        return tableStart;
    }

    /// <summary>The start of the line above the first delimiter row after the first line, or <see cref="int.MaxValue"/>.</summary>
    private static int FindTableStart(string paragraph)
    {
        var previousStart = 0;
        var lineStart = paragraph.IndexOf('\n', StringComparison.Ordinal) + 1;
        while (lineStart > 0 && lineStart <= paragraph.Length)
        {
            var lineEnd = paragraph.IndexOf('\n', lineStart);
            var line = paragraph.AsSpan(lineStart, (lineEnd < 0 ? paragraph.Length : lineEnd) - lineStart);
            if (line.Contains('-') && !line.ContainsAnyExcept(s_delimiterRow))
            {
                return previousStart;
            }
            previousStart = lineStart;
            lineStart = lineEnd + 1;
        }
        return int.MaxValue;
    }

    private sealed record TableCheck(string Text, int TableStart);
}
