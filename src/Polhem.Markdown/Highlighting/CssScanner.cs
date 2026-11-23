namespace Polhem.Markdown.Highlighting;

/// <summary>
/// Reads CSS: comments, strings, at-rules, selectors outside declaration blocks, and inside them property names, numbers
/// with their units, colors and <c>!important</c>.
/// </summary>
/// <remarks>NOTE: one forward pass, like <see cref="CodeScanner"/>.</remarks>
internal static class CssScanner
{
    public static void Scan(string code, TokenList tokens) => Scan(code, 0, code.Length, tokens);

    public static void Scan(string code, int start, int end, TokenList tokens)
    {
        var depth = 0;
        var declarationStart = true;
        var index = start;
        while (index < end)
        {
            var character = code[index];
            if (character == '/' && index + 1 < end && code[index + 1] == '*')
            {
                var close = code.IndexOf("*/", index + 2, end - index - 2, StringComparison.Ordinal);
                var commentEnd = close < 0 ? end : close + 2;
                tokens.Add(index, commentEnd, CodeTokenKind.Comment);
                index = commentEnd;
                continue;
            }
            if (character is '"' or '\'')
            {
                var stringEnd = StringEnd(code, index, end);
                tokens.Add(index, stringEnd, CodeTokenKind.String);
                index = stringEnd;
                continue;
            }
            switch (character)
            {
                case '{':
                    depth++;
                    declarationStart = true;
                    index++;
                    continue;
                case '}':
                    depth = Math.Max(depth - 1, 0);
                    declarationStart = true;
                    index++;
                    continue;
                case ';':
                    declarationStart = true;
                    index++;
                    continue;
                case '@' when index + 1 < end && char.IsLetter(code[index + 1]):
                    var ruleEnd = NameEnd(code, index + 1, end);
                    tokens.Add(index, ruleEnd, CodeTokenKind.Keyword);
                    index = ruleEnd;
                    continue;
                case '!' when index + 10 <= end && string.Compare(code, index, "!important", 0, 10, StringComparison.OrdinalIgnoreCase) == 0:
                    tokens.Add(index, index + 10, CodeTokenKind.Keyword);
                    index += 10;
                    continue;
            }

            if (depth > 0 && declarationStart && !char.IsWhiteSpace(character) && NestedRuleOpen(code, index, end) is { } open)
            {
                // A rule nested in a block, as inside @media or with CSS nesting: its selector runs up to its brace.
                while (index < open)
                {
                    index = Selector(code, index, open, tokens);
                }
                declarationStart = false;
            }
            else if (depth > 0)
            {
                index = Declaration(code, index, end, ref declarationStart, tokens);
            }
            else
            {
                index = Selector(code, index, end, tokens);
            }
        }
    }

    /// <summary>
    /// The brace that opens a nested rule starting at <paramref name="index"/>, or <see langword="null"/> when a semicolon
    /// or a closing brace comes first, as after a declaration.
    /// </summary>
    private static int? NestedRuleOpen(string code, int index, int end)
    {
        for (var position = index; position < end; position++)
        {
            switch (code[position])
            {
                case '{':
                    return position;
                case ';' or '}':
                    return null;
            }
        }
        return null;
    }

    private static int Declaration(string code, int index, int end, ref bool declarationStart, TokenList tokens)
    {
        var character = code[index];
        if (declarationStart && (char.IsLetter(character) || character == '-'))
        {
            var nameEnd = NameEnd(code, index, end);
            var after = nameEnd;
            while (after < end && code[after] is ' ' or '\t')
            {
                after++;
            }
            declarationStart = false;
            if (after < end && code[after] == ':')
            {
                tokens.Add(index, nameEnd, CodeTokenKind.Property);
            }
            return nameEnd;
        }
        if (!char.IsWhiteSpace(character))
        {
            declarationStart = false;
        }
        if (char.IsAsciiDigit(character) || (character == '.' && index + 1 < end && char.IsAsciiDigit(code[index + 1])))
        {
            var numberEnd = index + 1;
            while (numberEnd < end && (char.IsAsciiLetterOrDigit(code[numberEnd]) || code[numberEnd] is '.' or '%'))
            {
                numberEnd++;
            }
            tokens.Add(index, numberEnd, CodeTokenKind.Constant);
            return numberEnd;
        }
        if (character == '#' && index + 1 < end && char.IsAsciiHexDigit(code[index + 1]))
        {
            var colorEnd = index + 1;
            while (colorEnd < end && char.IsAsciiHexDigit(code[colorEnd]))
            {
                colorEnd++;
            }
            tokens.Add(index, colorEnd, CodeTokenKind.Constant);
            return colorEnd;
        }
        return char.IsLetter(character) || character == '-' ? NameEnd(code, index, end) : index + 1;
    }

    /// <summary>Selector names: elements, classes and ids, shown as tags; pseudo-classes stay plain.</summary>
    private static int Selector(string code, int index, int end, TokenList tokens)
    {
        var character = code[index];
        if (character == ':')
        {
            var pseudoEnd = index + 1;
            while (pseudoEnd < end && code[pseudoEnd] == ':')
            {
                pseudoEnd++;
            }
            return NameEnd(code, pseudoEnd, end);
        }
        if (char.IsLetter(character) || character is '.' or '#' or '*' || (character == '-' && index + 1 < end && char.IsLetter(code[index + 1])))
        {
            var nameEnd = character is '.' or '#' or '*' ? NameEnd(code, index + 1, end) : NameEnd(code, index, end);
            nameEnd = Math.Max(nameEnd, index + 1);
            tokens.Add(index, nameEnd, CodeTokenKind.Tag);
            return nameEnd;
        }
        return index + 1;
    }

    private static int StringEnd(string code, int index, int end)
    {
        var quote = code[index];
        var position = index + 1;
        while (position < end)
        {
            if (code[position] == '\\')
            {
                position += 2;
                continue;
            }
            if (code[position] == quote)
            {
                return position + 1;
            }
            if (code[position] == '\n')
            {
                return position;
            }
            position++;
        }
        return end;
    }

    private static int NameEnd(string code, int index, int end)
    {
        var position = index;
        while (position < end && (char.IsLetterOrDigit(code[position]) || code[position] is '-' or '_'))
        {
            position++;
        }
        return position;
    }
}
