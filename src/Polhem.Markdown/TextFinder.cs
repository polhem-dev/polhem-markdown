using System.Text.RegularExpressions;

namespace Polhem.Markdown;

/// <summary>
/// Finds a text in an editor's source with <see cref="MarkdownFindOptions"/>, and works out what replaces a match, for
/// find and replace in an editor (polhem-dev/polhem-markdown#97).
/// </summary>
internal sealed class TextFinder
{
    /// <summary>
    /// How long one regular expression may run on a text. A pattern that backtracks without end stops there, and the
    /// matches found until then are what the find has.
    /// </summary>
    private static readonly TimeSpan s_timeout = TimeSpan.FromSeconds(1);

    private readonly string _query;
    private readonly StringComparison _comparison;
    private readonly bool _wholeWord;
    private readonly Regex? _regex;

    /// <summary>Prepares to find <paramref name="query"/> with <paramref name="options"/>.</summary>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> has <see cref="MarkdownFindOptions.RegularExpression"/> and <paramref name="query"/> is
    /// not a valid regular expression.
    /// </exception>
    public TextFinder(string query, MarkdownFindOptions options)
    {
        _query = query;
        _comparison = options.HasFlag(MarkdownFindOptions.MatchCase) ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        _wholeWord = options.HasFlag(MarkdownFindOptions.WholeWord);
        if (options.HasFlag(MarkdownFindOptions.RegularExpression) && query.Length > 0)
        {
            var regexOptions = RegexOptions.CultureInvariant | RegexOptions.Multiline;
            if (!options.HasFlag(MarkdownFindOptions.MatchCase))
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }
            _regex = new Regex(query, regexOptions, s_timeout);
        }
    }

    /// <summary>
    /// The places the query occurs in <paramref name="text"/>, from the start and not overlapping, as the start and length
    /// of each. A regular expression's empty matches are left out: there is nothing to show or replace.
    /// </summary>
    public IReadOnlyList<(int Start, int Length)> FindAll(string text)
    {
        var matches = new List<(int Start, int Length)>();
        if (_query.Length == 0)
        {
            return matches;
        }
        if (_regex is not null)
        {
            try
            {
                for (var match = _regex.Match(text); match.Success; match = match.NextMatch())
                {
                    if (match.Length > 0 && IsWhole(text, match.Index, match.Length))
                    {
                        matches.Add((match.Index, match.Length));
                    }
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // The matches found before the timeout stay; see s_timeout.
                return matches;
            }
            return matches;
        }
        var index = text.IndexOf(_query, _comparison);
        while (index >= 0)
        {
            if (IsWhole(text, index, _query.Length))
            {
                matches.Add((index, _query.Length));
                index = text.IndexOf(_query, index + _query.Length, _comparison);
            }
            else
            {
                index = index + 1 < text.Length ? text.IndexOf(_query, index + 1, _comparison) : -1;
            }
        }
        return matches;
    }

    /// <summary>
    /// The text that takes the place of <paramref name="match"/> in <paramref name="text"/>: <paramref name="replacement"/>
    /// as it is, or, for a regular expression, with its references to the match's groups filled in.
    /// </summary>
    public string Replacement(string text, (int Start, int Length) match, string replacement)
    {
        if (_regex is null)
        {
            return replacement;
        }
        try
        {
            // NOTE: a regex run from where the match starts finds that match again, with its groups and with the text
            // before it still in view for lookbehinds and anchors.
            var found = _regex.Match(text, match.Start);
            return found.Success && found.Index == match.Start && found.Length == match.Length ? found.Result(replacement) : replacement;
        }
        catch (RegexMatchTimeoutException)
        {
            return replacement;
        }
    }

    private bool IsWhole(string text, int start, int length) =>
        !_wholeWord || (!IsWordCharacter(text, start - 1) && !IsWordCharacter(text, start + length));

    private static bool IsWordCharacter(string text, int index) =>
        index >= 0 && index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '_');
}
