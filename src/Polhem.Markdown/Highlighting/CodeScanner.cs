namespace Polhem.Markdown.Highlighting;

/// <summary>
/// Reads code in a language described by a <see cref="CodeLanguage"/>: comments, strings, numbers, keywords, constants
/// and variables, in one pass.
/// </summary>
/// <remarks>
/// NOTE: every branch moves forward past what it reads and never goes back, so the cost is linear in the length of the
/// code whatever it holds. A string or comment that is not closed, as in a block still arriving, runs to the end of the
/// line (an ordinary string) or of the code (anything that can span lines).
/// </remarks>
internal static class CodeScanner
{
    public static void Scan(string code, CodeLanguage language, TokenList tokens) => Scan(code, 0, code.Length, language, tokens);

    /// <summary>Scans <paramref name="code"/> from <paramref name="start"/> up to <paramref name="end"/>.</summary>
    public static void Scan(string code, int start, int end, CodeLanguage language, TokenList tokens)
    {
        var comparison = language.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var keywords = Rekey(language.Keywords, comparison);
        var constants = Rekey(language.Constants, comparison);
        var index = start;
        while (index < end)
        {
            index = Next(code, index, end, language, keywords, constants, tokens);
        }
    }

    private static IReadOnlySet<string> Rekey(IReadOnlySet<string> words, StringComparer comparison) =>
        words is HashSet<string> set && set.Comparer.Equals(comparison) ? set : new HashSet<string>(words, comparison);

    private static int Next(
        string code, int index, int end, CodeLanguage language, IReadOnlySet<string> keywords, IReadOnlySet<string> constants, TokenList tokens)
    {
        var current = code[index];

        if (BlockComment(code, index, end, language) is var (open, close))
        {
            var closeAt = code.IndexOf(close, index + open.Length, end - index - open.Length, StringComparison.Ordinal);
            var commentEnd = closeAt < 0 ? end : closeAt + close.Length;
            tokens.Add(index, commentEnd, CodeTokenKind.Comment);
            return commentEnd;
        }

        if (StartsAny(code, index, end, language.LineComments) is not null)
        {
            var lineEnd = LineEnd(code, index, end);
            tokens.Add(index, lineEnd, CodeTokenKind.Comment);
            return lineEnd;
        }

        if (StartsAny(code, index, end, language.LongQuotes) is { } longQuote)
        {
            var closeAt = code.IndexOf(longQuote, index + longQuote.Length, end - index - longQuote.Length, StringComparison.Ordinal);
            var stringEnd = closeAt < 0 ? end : closeAt + longQuote.Length;
            AddString(code, index, stringEnd, end, language, tokens);
            return stringEnd;
        }

        if (language.Quotes.Contains(current, StringComparison.Ordinal))
        {
            var stringEnd = StringEnd(code, index, end, language, verbatim: false);
            AddString(code, index, stringEnd, end, language, tokens);
            return stringEnd;
        }

        if (char.IsAsciiDigit(current) && !IsNameCharacter(code, index - 1, language))
        {
            var numberEnd = index + 1;
            while (numberEnd < end && (char.IsAsciiLetterOrDigit(code[numberEnd]) || code[numberEnd] is '_' or '.'))
            {
                numberEnd++;
            }
            tokens.Add(index, numberEnd, CodeTokenKind.Constant);
            return numberEnd;
        }

        if (language.VariablePrefix != '\0' && current == language.VariablePrefix && index + 1 < end)
        {
            if (code[index + 1] == '{')
            {
                var closeAt = code.IndexOf('}', index + 2, end - index - 2);
                var variableEnd = closeAt < 0 ? LineEnd(code, index, end) : closeAt + 1;
                tokens.Add(index, variableEnd, CodeTokenKind.Property);
                return variableEnd;
            }
            if (IsNameStart(code[index + 1], language))
            {
                var variableEnd = NameEnd(code, index + 1, end, language);
                var kind = constants.Contains(code[index..variableEnd]) ? CodeTokenKind.Constant : CodeTokenKind.Property;
                tokens.Add(index, variableEnd, kind);
                return variableEnd;
            }
        }

        if (language.PrefixStarts.Contains(current, StringComparison.Ordinal) && !IsNameCharacter(code, index - 1, language)
            && StringPrefix(code, index, end, language) is { } prefixed)
        {
            AddString(code, index, prefixed, end, language, tokens);
            return prefixed;
        }

        if (IsNameStart(current, language) && !IsNameCharacter(code, index - 1, language))
        {
            var nameEnd = NameEnd(code, index, end, language);
            var name = code[index..nameEnd];
            if (keywords.Contains(name))
            {
                tokens.Add(index, nameEnd, CodeTokenKind.Keyword);
            }
            else if (constants.Contains(name))
            {
                tokens.Add(index, nameEnd, CodeTokenKind.Constant);
            }
            return nameEnd;
        }

        return index + 1;
    }

    /// <summary>
    /// The end of a string that starts with a prefix (C#'s <c>$"</c> or <c>@"</c>, Python's <c>f"</c> or <c>r"</c>), or
    /// <see langword="null"/> when no prefix of the language is followed by a quote here.
    /// </summary>
    private static int? StringPrefix(string code, int index, int end, CodeLanguage language)
    {
        foreach (var (prefix, verbatim) in language.AllPrefixes)
        {
            var quoteAt = index + prefix.Length;
            if (quoteAt < end
                && string.CompareOrdinal(code, index, prefix, 0, prefix.Length) == 0
                && language.Quotes.Contains(code[quoteAt], StringComparison.Ordinal))
            {
                if (StartsAny(code, quoteAt, end, language.LongQuotes) is { } longQuote)
                {
                    var closeAt = code.IndexOf(longQuote, quoteAt + longQuote.Length, end - quoteAt - longQuote.Length, StringComparison.Ordinal);
                    return closeAt < 0 ? end : closeAt + longQuote.Length;
                }
                return StringEnd(code, quoteAt, end, language, verbatim);
            }
        }
        return null;
    }

    /// <summary>
    /// The end of a string whose opening quote is at <paramref name="index"/>. A doubled quote stays inside a verbatim
    /// string, and inside any string of a language without an escape character, as in SQL.
    /// </summary>
    private static int StringEnd(string code, int index, int end, CodeLanguage language, bool verbatim)
    {
        var quote = code[index];
        var multiline = verbatim || language.MultilineQuotes.Contains(quote, StringComparison.Ordinal);
        var escape = verbatim ? '\0' : language.Escape;
        var position = index + 1;
        while (position < end)
        {
            var character = code[position];
            if (escape != '\0' && character == escape)
            {
                position += 2;
                continue;
            }
            if (character == quote)
            {
                if (escape == '\0' && position + 1 < end && code[position + 1] == quote)
                {
                    position += 2;
                    continue;
                }
                return position + 1;
            }
            if (character == '\n' && !multiline)
            {
                return position;
            }
            position++;
        }
        return end;
    }

    /// <summary>Adds a string, or a key when the language marks strings before a colon as keys.</summary>
    private static void AddString(string code, int start, int stringEnd, int end, CodeLanguage language, TokenList tokens)
    {
        var kind = CodeTokenKind.String;
        if (language.KeysBeforeColon)
        {
            var after = stringEnd;
            while (after < end && code[after] is ' ' or '\t')
            {
                after++;
            }
            if (after < end && code[after] == ':')
            {
                kind = CodeTokenKind.Property;
            }
        }
        tokens.Add(start, Math.Min(stringEnd, end), kind);
    }

    private static (string Open, string Close)? BlockComment(string code, int index, int end, CodeLanguage language)
    {
        foreach (var pair in language.BlockComments)
        {
            if (pair.Open.Length <= end - index && string.CompareOrdinal(code, index, pair.Open, 0, pair.Open.Length) == 0)
            {
                return pair;
            }
        }
        return null;
    }

    private static string? StartsAny(string code, int index, int end, IEnumerable<string> prefixes)
    {
        foreach (var prefix in prefixes)
        {
            if (prefix.Length <= end - index && string.CompareOrdinal(code, index, prefix, 0, prefix.Length) == 0)
            {
                return prefix;
            }
        }
        return null;
    }

    public static int LineEnd(string code, int index, int end)
    {
        var newline = code.IndexOf('\n', index, end - index);
        return newline < 0 ? end : newline;
    }

    private static bool IsNameStart(char character, CodeLanguage language) =>
        char.IsLetter(character) || character == '_' || language.NameCharacters.Contains(character, StringComparison.Ordinal);

    private static bool IsNameCharacter(string code, int index, CodeLanguage language) =>
        index >= 0 && (char.IsLetterOrDigit(code[index]) || code[index] == '_' || language.NameCharacters.Contains(code[index], StringComparison.Ordinal));

    private static int NameEnd(string code, int index, int end, CodeLanguage language)
    {
        var position = index;
        while (position < end && (char.IsLetterOrDigit(code[position]) || code[position] == '_'
            || language.NameCharacters.Contains(code[position], StringComparison.Ordinal)))
        {
            position++;
        }
        return position;
    }
}
