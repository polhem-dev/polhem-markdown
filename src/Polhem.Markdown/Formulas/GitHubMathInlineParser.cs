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
        return text.AsSpan(start, end - start).Contains('|') && processor.Block is ParagraphBlock && IsTable(text);
    }

    /// <summary>The last paragraph checked, so a paragraph with many formulas is scanned once.</summary>
    private TableCheck? _lastCheck;

    /// <summary>
    /// Whether the paragraph's text, which the inline parsers read, holds a pipe table: one of its lines is a delimiter
    /// row such as <c>|---|:--:|</c>. A table may follow lines of text in the same paragraph.
    /// </summary>
    /// <remarks>
    /// NOTE: the answer is kept for the paragraph's text, since each formula with a bar asks again; scanning every time
    /// would cost the length of the paragraph per formula. Parsers run on several threads at once share this one, so the
    /// check is kept in one immutable object; a race only scans again.
    /// </remarks>
    private bool IsTable(string paragraph)
    {
        var last = Volatile.Read(ref _lastCheck);
        if (last is not null && ReferenceEquals(last.Text, paragraph))
        {
            return last.IsTable;
        }
        var isTable = HasDelimiterRow(paragraph);
        Volatile.Write(ref _lastCheck, new TableCheck(paragraph, isTable));
        return isTable;
    }

    /// <summary>Whether a line after the first consists of dashes, colons, bars and spaces, with a dash: a delimiter row.</summary>
    private static bool HasDelimiterRow(string paragraph)
    {
        var lineStart = paragraph.IndexOf('\n', StringComparison.Ordinal) + 1;
        while (lineStart > 0 && lineStart <= paragraph.Length)
        {
            var lineEnd = paragraph.IndexOf('\n', lineStart);
            var line = paragraph.AsSpan(lineStart, (lineEnd < 0 ? paragraph.Length : lineEnd) - lineStart);
            if (line.Contains('-') && !line.ContainsAnyExcept("-:| \t\r"))
            {
                return true;
            }
            lineStart = lineEnd + 1;
        }
        return false;
    }

    private sealed record TableCheck(string Text, bool IsTable);
}
