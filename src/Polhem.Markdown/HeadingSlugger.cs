using System.Globalization;
using System.Text;

namespace Polhem.Markdown;

/// <summary>
/// Gives headings the ids GitHub gives them: the text in lower case, with everything but letters, digits, marks,
/// underscores, hyphens and spaces dropped and spaces turned into hyphens; a repeated id gets <c>-1</c>, <c>-2</c> and so
/// on, skipping any id already taken.
/// </summary>
/// <remarks>
/// NOTE: this follows github-slugger rather than Markdig's auto identifiers, whose numbering of repeats tries every
/// number from one on each repeat. A reply with thousands of headings of the same name then took seconds to parse,
/// on every chunk while it streamed; the counts kept here make each id cost the same however many came before.
/// </remarks>
internal sealed class HeadingSlugger
{
    private readonly Dictionary<string, int> _occurrences = [];

    /// <summary>The id of the next heading of the document, whose plain text is <paramref name="text"/>.</summary>
    public string Next(string text)
    {
        var slug = Slug(text);
        var id = slug;
        while (_occurrences.ContainsKey(id))
        {
            var count = _occurrences[slug] + 1;
            _occurrences[slug] = count;
            id = slug + "-" + count.ToString(CultureInfo.InvariantCulture);
        }
        _occurrences[id] = 0;
        return id;
    }

    private static string Slug(string text)
    {
        var slug = new StringBuilder(text.Length);
        foreach (var rune in text.ToLowerInvariant().EnumerateRunes())
        {
            switch (Rune.GetUnicodeCategory(rune))
            {
                case UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or UnicodeCategory.TitlecaseLetter
                    or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter
                    or UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark
                    or UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.OtherNumber
                    or UnicodeCategory.ConnectorPunctuation:
                    slug.Append(rune.ToString());
                    break;
                case UnicodeCategory.DashPunctuation when rune.Value == '-':
                    slug.Append('-');
                    break;
                case UnicodeCategory.SpaceSeparator when rune.Value == ' ':
                    slug.Append('-');
                    break;
            }
        }
        return slug.ToString();
    }
}
