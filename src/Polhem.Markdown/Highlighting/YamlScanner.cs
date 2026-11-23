namespace Polhem.Markdown.Highlighting;

/// <summary>
/// Reads YAML a line at a time: comments, keys, strings, constants, document markers, and the lines of a block scalar
/// (<c>|</c> or <c>&gt;</c>), which stay plain text.
/// </summary>
/// <remarks>NOTE: each line is read once from left to right, so the cost is linear in the length of the code.</remarks>
internal static class YamlScanner
{
    private static readonly HashSet<string> s_constants =
    [
        "true", "false", "null", "~", "yes", "no", "on", "off", "True", "False", "Null", "TRUE", "FALSE", "NULL",
    ];

    public static void Scan(string code, TokenList tokens)
    {
        var blockIndent = -1;
        var lineStart = 0;
        while (lineStart < code.Length)
        {
            var lineEnd = CodeScanner.LineEnd(code, lineStart, code.Length);
            var indent = lineStart;
            while (indent < lineEnd && code[indent] == ' ')
            {
                indent++;
            }
            var column = indent - lineStart;

            // A block scalar takes every following line indented further than the key that opened it, and blank lines.
            if (blockIndent >= 0 && (indent == lineEnd || column > blockIndent))
            {
                lineStart = lineEnd + 1;
                continue;
            }
            blockIndent = -1;

            if (Line(code, indent, lineEnd, tokens))
            {
                blockIndent = column;
            }
            lineStart = lineEnd + 1;
        }
    }

    /// <summary>Reads one line from its first non-space character; returns whether it opens a block scalar.</summary>
    private static bool Line(string code, int index, int end, TokenList tokens)
    {
        if (index + 3 <= end && (string.CompareOrdinal(code, index, "---", 0, 3) == 0 || string.CompareOrdinal(code, index, "...", 0, 3) == 0)
            && (index + 3 == end || char.IsWhiteSpace(code[index + 3])))
        {
            tokens.Add(index, index + 3, CodeTokenKind.Keyword);
            index += 3;
        }
        while (index < end && (code[index] is ' ' or '\t' || (code[index] == '-' && (index + 1 == end || code[index + 1] == ' '))))
        {
            index++;
        }
        if (index >= end)
        {
            return false;
        }
        if (code[index] == '#')
        {
            tokens.Add(index, end, CodeTokenKind.Comment);
            return false;
        }

        var keyEnd = KeyEnd(code, index, end);
        if (keyEnd >= 0)
        {
            tokens.Add(index, keyEnd, CodeTokenKind.Property);
            index = keyEnd + 1;
        }
        return Value(code, index, end, tokens);
    }

    /// <summary>The index of the colon that ends a key starting at <paramref name="index"/>, or -1.</summary>
    private static int KeyEnd(string code, int index, int end)
    {
        if (code[index] is '"' or '\'')
        {
            var close = code.IndexOf(code[index], index + 1, end - index - 1);
            return close >= 0 && close + 1 < end && code[close + 1] == ':' && (close + 2 == end || code[close + 2] == ' ') ? close + 1 : -1;
        }
        for (var position = index; position < end; position++)
        {
            if (code[position] == ':' && (position + 1 == end || code[position + 1] is ' ' or '\t'))
            {
                return position;
            }
            if (code[position] == '#' && position > index && code[position - 1] == ' ')
            {
                return -1;
            }
        }
        return -1;
    }

    private static bool Value(string code, int index, int end, TokenList tokens)
    {
        while (index < end && code[index] is ' ' or '\t')
        {
            index++;
        }
        if (index >= end)
        {
            return false;
        }
        var character = code[index];
        if (character is '|' or '>')
        {
            return true;
        }
        if (character is '"' or '\'')
        {
            var close = code.IndexOf(character, index + 1, end - index - 1);
            var stringEnd = close < 0 ? end : close + 1;
            tokens.Add(index, stringEnd, CodeTokenKind.String);
            index = stringEnd;
        }
        else
        {
            var comment = CommentStart(code, index, end);
            var valueEnd = comment;
            while (valueEnd > index && char.IsWhiteSpace(code[valueEnd - 1]))
            {
                valueEnd--;
            }
            var value = code.AsSpan(index, valueEnd - index);
            if (s_constants.GetAlternateLookup<ReadOnlySpan<char>>().Contains(value) || IsNumber(value))
            {
                tokens.Add(index, valueEnd, CodeTokenKind.Constant);
            }
            index = comment;
        }
        var trailing = CommentStart(code, index, end);
        tokens.Add(trailing, end, CodeTokenKind.Comment);
        return false;
    }

    /// <summary>Where a comment starts: a <c>#</c> after a space, or <paramref name="end"/> when there is none.</summary>
    private static int CommentStart(string code, int index, int end)
    {
        for (var position = index; position < end; position++)
        {
            if (code[position] == '#' && (position == index || code[position - 1] is ' ' or '\t'))
            {
                return position;
            }
        }
        return end;
    }

    private static bool IsNumber(ReadOnlySpan<char> value) =>
        value.Length > 0 && (char.IsAsciiDigit(value[0]) || (value.Length > 1 && value[0] is '-' or '+' or '.' && char.IsAsciiDigit(value[1])))
        && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _);
}
