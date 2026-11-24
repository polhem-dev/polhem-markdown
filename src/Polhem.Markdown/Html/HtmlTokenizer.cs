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

        // NOTE: each step either moves past what it read or ends the loop, so no character is read twice except by the
        // one search that finds where its construct ends.
        while (index < html.Length && html.IndexOf('<', index) is var open and >= 0)
        {
            var next = Construct(html, open, tokens, ref textStart);
            if (next < 0)
            {
                // A construct cut off by the end of the HTML: the rest is text, or already skipped.
                break;
            }
            index = next;
        }
        AddText(html, textStart, html.Length, tokens);
        return tokens;
    }

    /// <summary>
    /// Reads what starts with the <c>&lt;</c> at <paramref name="open"/> and returns where to go on, or -1 when nothing
    /// after it is a tag. Text before a construct is added first, and <paramref name="textStart"/> moves past it.
    /// </summary>
    private static int Construct(string html, int open, List<HtmlToken> tokens, ref int textStart)
    {
        var next = open + 1 < html.Length ? html[open + 1] : '\0';
        if (Matches(html, open, "<!--"))
        {
            AddText(html, textStart, open, tokens);
            var close = html.IndexOf("-->", open + 4, StringComparison.Ordinal);
            return textStart = close < 0 ? html.Length : close + 3;
        }
        if (next is '!' or '?')
        {
            AddText(html, textStart, open, tokens);
            var close = html.IndexOf('>', open + 2);
            return textStart = close < 0 ? html.Length : close + 1;
        }
        if (next == '/' && open + 2 < html.Length && char.IsAsciiLetter(html[open + 2]))
        {
            return EndTag(html, open, tokens, ref textStart);
        }
        if (char.IsAsciiLetter(next))
        {
            return StartTag(html, open, tokens, ref textStart);
        }
        return open + 1;
    }

    private static int EndTag(string html, int open, List<HtmlToken> tokens, ref int textStart)
    {
        var name = ReadName(html, open + 2);
        var close = html.IndexOf('>', open + 2 + name.Length);
        if (close < 0)
        {
            return -1;
        }
        AddText(html, textStart, open, tokens);
        tokens.Add(new HtmlToken(HtmlTokenKind.EndTag, open, close + 1 - open, name, html[open..(close + 1)], s_noAttributes));
        return textStart = close + 1;
    }

    private static int StartTag(string html, int open, List<HtmlToken> tokens, ref int textStart)
    {
        if (!TryReadStartTag(html, open, out var tag))
        {
            return -1;
        }
        AddText(html, textStart, open, tokens);
        textStart = tag.End;
        if (!HtmlElements.IsDiscarded(tag.Name))
        {
            tokens.Add(tag);
            return tag.End;
        }

        // An element whose content is dropped is skipped with its end tag; without one, its start tag is kept so the
        // composer drops what follows, and the rest of this HTML is skipped.
        var end = FindEndTag(html, tag.End, tag.Name);
        if (end < 0)
        {
            tokens.Add(tag);
            textStart = html.Length;
            return -1;
        }
        return textStart = end;
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
            index = Skip(html, index, c => char.IsWhiteSpace(c) || c == '/');
            if (index >= html.Length)
            {
                return false;
            }
            if (html[index] == '>')
            {
                tag = new HtmlToken(HtmlTokenKind.StartTag, open, index + 1 - open, name, html[open..(index + 1)], attributes);
                return true;
            }
            index = ReadAttribute(html, index, attributes);
            if (index < 0)
            {
                return false;
            }
        }
    }

    /// <summary>Reads one attribute at <paramref name="start"/> and returns the index after it, or -1 when the HTML ends inside it.</summary>
    private static int ReadAttribute(string html, int start, Dictionary<string, string> attributes)
    {
        var index = Skip(html, start + 1, c => !char.IsWhiteSpace(c) && c is not ('/' or '>' or '='));
        var name = html[start..index].ToLowerInvariant();
        index = Skip(html, index, char.IsWhiteSpace);

        var value = "";
        if (index < html.Length && html[index] == '=')
        {
            index = Skip(html, index + 1, char.IsWhiteSpace);
            if (index >= html.Length)
            {
                return -1;
            }
            if (html[index] is '"' or '\'')
            {
                var close = html.IndexOf(html[index], index + 1);
                if (close < 0)
                {
                    return -1;
                }
                value = html[(index + 1)..close];
                index = close + 1;
            }
            else
            {
                var valueStart = index;
                index = Skip(html, index, c => !char.IsWhiteSpace(c) && c != '>');
                value = html[valueStart..index];
            }
        }

        // As in a browser, the first of two attributes with the same name wins.
        attributes.TryAdd(name, WebUtility.HtmlDecode(value));
        return index;
    }

    private static int Skip(string html, int index, Func<char, bool> skipped)
    {
        while (index < html.Length && skipped(html[index]))
        {
            index++;
        }
        return index;
    }

    /// <summary>The index just past the end tag of <paramref name="name"/> at or after <paramref name="start"/>, or -1.</summary>
    private static int FindEndTag(string html, int start, string name)
    {
        var index = start;
        while (html.IndexOf("</" + name, index, StringComparison.OrdinalIgnoreCase) is var close and >= 0)
        {
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
        return -1;
    }
}
