using System.Text.RegularExpressions;
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
internal sealed partial class GitHubMathInlineParser : MathInlineParser
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

    private static bool IsRefused(MathInline math, InlineProcessor processor, string text, int start, int end)
    {
        var count = math.DelimiterCount;
        if (count == 1 && (char.IsWhiteSpace(text[start + 1]) || char.IsWhiteSpace(text[end - 2])))
        {
            return true;
        }
        return text.AsSpan(start, end - start).Contains('|') && processor.Block is ParagraphBlock && IsTable(text);
    }

    /// <summary>
    /// Whether the paragraph's text, which the inline parsers read, is a pipe table: its second line is a delimiter row
    /// such as <c>|---|:--:|</c>.
    /// </summary>
    private static bool IsTable(string paragraph)
    {
        var first = paragraph.IndexOf('\n', StringComparison.Ordinal);
        if (first < 0)
        {
            return false;
        }
        var second = paragraph.IndexOf('\n', first + 1);
        var line = second < 0 ? paragraph[(first + 1)..] : paragraph[(first + 1)..second];
        return DelimiterRow().IsMatch(line);
    }

    [GeneratedRegex(@"^\s*\|?\s*:?-+:?\s*(\|\s*:?-+:?\s*)*\|?\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex DelimiterRow();
}
