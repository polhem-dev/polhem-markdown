namespace Polhem.Markdown.Highlighting;

/// <summary>
/// Reads code in a language described by a <see cref="CodeLanguage"/>: comments, strings, numbers, keywords, constants
/// and variables, in one pass.
/// </summary>
/// <remarks>
/// NOTE: every reader moves forward past what it reads and never goes back, and a search for a closing delimiter that
/// fails ends what it closes at the end of the code (or of the line, for what cannot span lines), so the cost is linear
/// in the length of the code whatever it holds. A string or comment that is not closed, as in a block still arriving,
/// runs to the end of the line (an ordinary string) or of the code (anything that can span lines).
/// </remarks>
internal sealed class CodeScanner
{
    private readonly string _code;
    private readonly int _end;
    private readonly CodeLanguage _language;
    private readonly HashSet<string> _keywords;
    private readonly HashSet<string> _constants;
    private readonly TokenList _tokens;

    private CodeScanner(string code, int end, CodeLanguage language, TokenList tokens)
    {
        _code = code;
        _end = end;
        _language = language;
        _tokens = tokens;
        var comparison = language.IgnoreCase ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        _keywords = Rekey(language.Keywords, comparison);
        _constants = Rekey(language.Constants, comparison);
    }

    public static void Scan(string code, CodeLanguage language, TokenList tokens) => Scan(code, 0, code.Length, language, tokens);

    /// <summary>Scans <paramref name="code"/> from <paramref name="start"/> up to <paramref name="end"/>.</summary>
    public static void Scan(string code, int start, int end, CodeLanguage language, TokenList tokens)
    {
        var scanner = new CodeScanner(code, end, language, tokens);
        var index = start;
        while (index < end)
        {
            index = scanner.Next(index);
        }
    }

    public static int LineEnd(string code, int index, int end)
    {
        var newline = code.IndexOf('\n', index, end - index);
        return newline < 0 ? end : newline;
    }

    // The language's sets are built with the comparer it needs, so this returns them as they are.
    private static HashSet<string> Rekey(IReadOnlySet<string> words, StringComparer comparison) =>
        words is HashSet<string> set && set.Comparer.Equals(comparison) ? set : new HashSet<string>(words, comparison);

    /// <summary>Reads what starts at <paramref name="index"/> and returns the index after it.</summary>
    private int Next(int index) =>
        BlockComment(index) ?? LineComment(index) ?? LongString(index) ?? QuotedString(index) ?? Number(index)
        ?? Variable(index) ?? PrefixedString(index) ?? Name(index) ?? index + 1;

    private int? BlockComment(int index)
    {
        var comments = _language.BlockComments;
        for (var i = 0; i < comments.Count; i++)
        {
            var (open, close) = comments[i];
            if (StartsWith(index, open))
            {
                var commentEnd = Closing(index + open.Length, close, _end);
                _tokens.Add(index, commentEnd, CodeTokenKind.Comment);
                return commentEnd;
            }
        }
        return null;
    }

    private int? LineComment(int index)
    {
        if (StartsAny(index, _language.LineComments) is null)
        {
            return null;
        }
        var lineEnd = LineEnd(_code, index, _end);
        _tokens.Add(index, lineEnd, CodeTokenKind.Comment);
        return lineEnd;
    }

    private int? LongString(int index)
    {
        if (LongQuoteAt(index) is not { } longQuote)
        {
            return null;
        }
        var stringEnd = Closing(index + longQuote.Length, longQuote, _end);
        AddString(index, stringEnd);
        return stringEnd;
    }

    private int? QuotedString(int index)
    {
        if (!_language.Quotes.Contains(_code[index], StringComparison.Ordinal))
        {
            return null;
        }
        var stringEnd = StringEnd(index, verbatim: false);
        AddString(index, stringEnd);
        return stringEnd;
    }

    private int? Number(int index)
    {
        if (!char.IsAsciiDigit(_code[index]) || IsNameCharacter(index - 1))
        {
            return null;
        }
        var numberEnd = index + 1;
        while (numberEnd < _end && (char.IsAsciiLetterOrDigit(_code[numberEnd]) || _code[numberEnd] is '_' or '.'))
        {
            numberEnd++;
        }
        _tokens.Add(index, numberEnd, CodeTokenKind.Constant);
        return numberEnd;
    }

    /// <summary>A shell variable: <c>$name</c>, or <c>${...}</c> up to its brace on the same line.</summary>
    private int? Variable(int index)
    {
        if (_language.VariablePrefix == '\0' || _code[index] != _language.VariablePrefix || index + 1 >= _end)
        {
            return null;
        }
        if (_code[index + 1] == '{')
        {
            // The search stops at the end of the line: one unclosed `${` per line would otherwise search the rest of the
            // code from every line.
            var lineEnd = LineEnd(_code, index, _end);
            var closeAt = _code.IndexOf('}', index + 2, lineEnd - index - 2);
            var variableEnd = closeAt < 0 ? lineEnd : closeAt + 1;
            _tokens.Add(index, variableEnd, CodeTokenKind.Property);
            return variableEnd;
        }
        if (!IsNameStart(_code[index + 1]))
        {
            return null;
        }
        var nameEnd = NameEnd(index + 1);
        var kind = _constants.Contains(_code[index..nameEnd]) ? CodeTokenKind.Constant : CodeTokenKind.Property;
        _tokens.Add(index, nameEnd, kind);
        return nameEnd;
    }

    /// <summary>
    /// A string that starts with a prefix: C#'s <c>$"</c> or <c>@"</c>, Python's <c>f"</c> or <c>r"</c>.
    /// </summary>
    private int? PrefixedString(int index)
    {
        if (!_language.PrefixStarts.Contains(_code[index], StringComparison.Ordinal) || IsNameCharacter(index - 1))
        {
            return null;
        }
        var prefixes = _language.AllPrefixes;
        for (var i = 0; i < prefixes.Count; i++)
        {
            var (prefix, verbatim) = prefixes[i];
            var quoteAt = index + prefix.Length;
            if (quoteAt < _end && StartsWith(index, prefix) && _language.Quotes.Contains(_code[quoteAt], StringComparison.Ordinal))
            {
                var stringEnd = LongQuoteAt(quoteAt) is { } longQuote
                    ? Closing(quoteAt + longQuote.Length, longQuote, _end)
                    : StringEnd(quoteAt, verbatim);
                AddString(index, stringEnd);
                return stringEnd;
            }
        }
        return null;
    }

    private int? Name(int index)
    {
        if (!IsNameStart(_code[index]) || IsNameCharacter(index - 1))
        {
            return null;
        }
        var nameEnd = NameEnd(index);
        var name = _code.AsSpan(index, nameEnd - index);
        if (_keywords.GetAlternateLookup<ReadOnlySpan<char>>().Contains(name))
        {
            _tokens.Add(index, nameEnd, CodeTokenKind.Keyword);
        }
        else if (_constants.GetAlternateLookup<ReadOnlySpan<char>>().Contains(name))
        {
            _tokens.Add(index, nameEnd, CodeTokenKind.Constant);
        }
        return nameEnd;
    }

    /// <summary>
    /// The end of a string whose opening quote is at <paramref name="index"/>. A doubled quote stays inside a verbatim
    /// string, and inside any string of a language without an escape character, as in SQL.
    /// </summary>
    private int StringEnd(int index, bool verbatim)
    {
        var quote = _code[index];
        var multiline = verbatim || _language.MultilineQuotes.Contains(quote, StringComparison.Ordinal);
        var escape = verbatim ? '\0' : _language.Escape;
        var position = index + 1;
        while (position < _end)
        {
            var character = _code[position];
            if (escape != '\0' && character == escape)
            {
                position += 2;
            }
            else if (character == quote && escape == '\0' && position + 1 < _end && _code[position + 1] == quote)
            {
                position += 2;
            }
            else if (character == quote)
            {
                return position + 1;
            }
            else if (character == '\n' && !multiline)
            {
                return position;
            }
            else
            {
                position++;
            }
        }
        return _end;
    }

    /// <summary>Adds a string, or a key when the language marks strings before a colon as keys.</summary>
    private void AddString(int start, int stringEnd)
    {
        stringEnd = Math.Min(stringEnd, _end);
        var kind = CodeTokenKind.String;
        if (_language.KeysBeforeColon)
        {
            var after = stringEnd;
            while (after < _end && _code[after] is ' ' or '\t')
            {
                after++;
            }
            if (after < _end && _code[after] == ':')
            {
                kind = CodeTokenKind.Property;
            }
        }
        _tokens.Add(start, stringEnd, kind);
    }

    private string? LongQuoteAt(int index) => StartsAny(index, _language.LongQuotes);

    // A loop rather than LINQ: it runs at almost every character, and a lambda would allocate each time.
    private string? StartsAny(int index, IReadOnlyList<string> prefixes)
    {
        for (var i = 0; i < prefixes.Count; i++)
        {
            if (StartsWith(index, prefixes[i]))
            {
                return prefixes[i];
            }
        }
        return null;
    }

    /// <summary>The index after <paramref name="close"/> found from <paramref name="from"/>, or <paramref name="limit"/>.</summary>
    private int Closing(int from, string close, int limit)
    {
        from = Math.Min(from, limit);
        var closeAt = _code.IndexOf(close, from, limit - from, StringComparison.Ordinal);
        return closeAt < 0 ? limit : closeAt + close.Length;
    }

    private bool StartsWith(int index, string text) =>
        text.Length <= _end - index && string.CompareOrdinal(_code, index, text, 0, text.Length) == 0;

    private bool IsNameStart(char character) =>
        char.IsLetter(character) || character == '_' || _language.NameCharacters.Contains(character, StringComparison.Ordinal);

    private bool IsNameCharacter(int index) =>
        index >= 0 && (char.IsLetterOrDigit(_code[index]) || _code[index] == '_' || _language.NameCharacters.Contains(_code[index], StringComparison.Ordinal));

    private int NameEnd(int index)
    {
        var position = index;
        while (position < _end && (char.IsLetterOrDigit(_code[position]) || _code[position] == '_'
            || _language.NameCharacters.Contains(_code[position], StringComparison.Ordinal)))
        {
            position++;
        }
        return position;
    }
}
