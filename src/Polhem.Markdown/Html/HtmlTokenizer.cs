using System.Net;

namespace Polhem.Markdown.Html;

/// <summary>
/// Splits HTML into text and tags. It reads tags the way a browser does for the common cases, never builds markup from
/// what it reads, and takes time linear in the length of the HTML.
/// </summary>
/// <remarks>
/// Comments, declarations and processing instructions are dropped. The content of the elements whose content is dropped
/// (see <see cref="HtmlElements.IsDiscarded"/>) is skipped up to their end tag; when there is none, the start tag is
/// returned and the rest of the HTML is skipped. A tag cut off by the end of the HTML is text, as is everything after it.
/// </remarks>
internal static class HtmlTokenizer
{
    private static readonly IReadOnlyDictionary<string, string> s_noAttributes = new Dictionary<string, string>(StringComparer.Ordinal);

    public static List<HtmlToken> Tokenize(string html)
    {
        ArgumentNullException.ThrowIfNull(html);

        var tokens = new List<HtmlToken>();
        var textStart = 0;
        var index = 0;
        while (index < html.Length)
        {
            var open = html.IndexOf('<', index);
            if (open < 0)
            {
                break;
            }

            // NOTE: every branch below either moves past what it read or ends the loop, so no character is read twice
            // except by the one search that finds where its construct ends.
            if (Matches(html, open, "<!--"))
            {
                AddText(html, textStart, open, tokens);
                var close = html.IndexOf("-->", open + 4, StringComparison.Ordinal);
                index = textStart = close < 0 ? html.Length : close + 3;
            }
            else if (open + 1 < html.Length && html[open + 1] is '!' or '?')
            {
                AddText(html, textStart, open, tokens);
                var close = html.IndexOf('>', open + 2);
                index = textStart = close < 0 ? html.Length : close + 1;
            }
            else if (open + 2 < html.Length && html[open + 1] == '/' && char.IsAsciiLetter(html[open + 2]))
            {
                var name = ReadName(html, open + 2);
                var close = html.IndexOf('>', open + 2 + name.Length);
                if (close < 0)
                {
                    break;
                }
                AddText(html, textStart, open, tokens);
                tokens.Add(new HtmlToken(HtmlTokenKind.EndTag, open, close + 1 - open, name, "", s_noAttributes));
                index = textStart = close + 1;
            }
            else if (open + 1 < html.Length && char.IsAsciiLetter(html[open + 1]))
            {
                if (!TryReadStartTag(html, open, out var tag))
                {
                    break;
                }
                AddText(html, textStart, open, tokens);
                tokens.Add(tag);
                index = textStart = tag.End;
                if (HtmlElements.IsDiscarded(tag.Name))
                {
                    var end = FindEndTag(html, tag.End, tag.Name);
                    if (end < 0)
                    {
                        return tokens;
                    }
                    tokens.RemoveAt(tokens.Count - 1);
                    index = textStart = end;
                }
            }
            else
            {
                index = open + 1;
            }
        }
        AddText(html, textStart, html.Length, tokens);
        return tokens;
    }

    private static void AddText(string html, int start, int end, List<HtmlToken> tokens)
    {
        if (end > start)
        {
            var text = WebUtility.HtmlDecode(html[start..end]);
            tokens.Add(new HtmlToken(HtmlTokenKind.Text, start, end - start, "", text, s_noAttributes));
        }
    }

    private static bool Matches(string html, int index, string value) =>
        string.CompareOrdinal(html, index, value, 0, value.Length) == 0;

    private static string ReadName(string html, int start)
    {
        var end = start;
        while (end < html.Length && (char.IsAsciiLetterOrDigit(html[end]) || html[end] == '-'))
        {
            end++;
        }
        return html[start..end].ToLowerInvariant();
    }

    /// <summary>Reads a start tag at <paramref name="open"/>; it fails only when the HTML ends inside the tag.</summary>
    private static bool TryReadStartTag(string html, int open, out HtmlToken tag)
    {
        tag = null!;
        var name = ReadName(html, open + 1);
        var attributes = new Dictionary<string, string>(StringComparer.Ordinal);
        var index = open + 1 + name.Length;
        while (true)
        {
            while (index < html.Length && (char.IsWhiteSpace(html[index]) || html[index] == '/'))
            {
                index++;
            }
            if (index >= html.Length)
            {
                return false;
            }
            if (html[index] == '>')
            {
                tag = new HtmlToken(HtmlTokenKind.StartTag, open, index + 1 - open, name, "", attributes);
                return true;
            }

            var nameStart = index;
            index++;
            while (index < html.Length && !char.IsWhiteSpace(html[index]) && html[index] is not ('/' or '>' or '='))
            {
                index++;
            }
            var attribute = html[nameStart..index].ToLowerInvariant();
            while (index < html.Length && char.IsWhiteSpace(html[index]))
            {
                index++;
            }

            var value = "";
            if (index < html.Length && html[index] == '=')
            {
                index++;
                while (index < html.Length && char.IsWhiteSpace(html[index]))
                {
                    index++;
                }
                if (index >= html.Length)
                {
                    return false;
                }
                if (html[index] is '"' or '\'')
                {
                    var close = html.IndexOf(html[index], index + 1);
                    if (close < 0)
                    {
                        return false;
                    }
                    value = html[(index + 1)..close];
                    index = close + 1;
                }
                else
                {
                    var valueStart = index;
                    while (index < html.Length && !char.IsWhiteSpace(html[index]) && html[index] != '>')
                    {
                        index++;
                    }
                    value = html[valueStart..index];
                }
            }

            // As in a browser, the first of two attributes with the same name wins.
            attributes.TryAdd(attribute, WebUtility.HtmlDecode(value));
        }
    }

    /// <summary>The index just past the end tag of <paramref name="name"/> at or after <paramref name="start"/>, or -1.</summary>
    private static int FindEndTag(string html, int start, string name)
    {
        var index = start;
        while (true)
        {
            var close = html.IndexOf("</" + name, index, StringComparison.OrdinalIgnoreCase);
            if (close < 0)
            {
                return -1;
            }
            var after = close + 2 + name.Length;
            if (after >= html.Length)
            {
                return -1;
            }
            if (char.IsWhiteSpace(html[after]) || html[after] is '/' or '>')
            {
                var end = html.IndexOf('>', after);
                return end < 0 ? -1 : end + 1;
            }
            index = close + 1;
        }
    }
}
