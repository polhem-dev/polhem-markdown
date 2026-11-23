namespace Polhem.Markdown.Highlighting;

/// <summary>
/// Reads CSS: comments, strings, at-rules, selectors outside declaration blocks and of rules nested in them, and inside
/// blocks property names, numbers with their units, colors and <c>!important</c>.
/// </summary>
/// <remarks>
/// NOTE: one forward pass, like <see cref="CodeScanner"/>. The only look-ahead, for a nested rule, runs from the start of
/// a declaration to its first <c>{</c>, <c>;</c> or <c>}</c>, and what it passes over is read next, so no character is
/// looked at more than twice.
/// </remarks>
internal sealed class CssScanner
{
    private readonly string _code;
    private readonly int _end;
    private readonly TokenList _tokens;
    private int _depth;
    private bool _declarationStart = true;

    private CssScanner(string code, int end, TokenList tokens)
    {
        _code = code;
        _end = end;
        _tokens = tokens;
    }

    public static void Scan(string code, TokenList tokens) => Scan(code, 0, code.Length, tokens);

    public static void Scan(string code, int start, int end, TokenList tokens)
    {
        var scanner = new CssScanner(code, end, tokens);
        var index = start;
        while (index < end)
        {
            index = scanner.Next(index);
        }
    }

    private int Next(int index) =>
        Comment(index) ?? QuotedString(index) ?? Punctuation(index) ?? AtRule(index) ?? Important(index)
        ?? NestedRule(index) ?? (_depth > 0 ? Declaration(index) : Selector(index, _end));

    private int? Comment(int index)
    {
        if (_code[index] != '/' || index + 1 >= _end || _code[index + 1] != '*')
        {
            return null;
        }
        var close = _code.IndexOf("*/", index + 2, _end - index - 2, StringComparison.Ordinal);
        var commentEnd = close < 0 ? _end : close + 2;
        _tokens.Add(index, commentEnd, CodeTokenKind.Comment);
        return commentEnd;
    }

    private int? QuotedString(int index)
    {
        if (_code[index] is not ('"' or '\''))
        {
            return null;
        }
        var stringEnd = StringEnd(index);
        _tokens.Add(index, stringEnd, CodeTokenKind.String);
        return stringEnd;
    }

    /// <summary>Braces and semicolons, which open and close blocks and start declarations.</summary>
    private int? Punctuation(int index)
    {
        switch (_code[index])
        {
            case '{':
                _depth++;
                break;
            case '}':
                _depth = Math.Max(_depth - 1, 0);
                break;
            case ';':
                break;
            default:
                return null;
        }
        _declarationStart = true;
        return index + 1;
    }

    private int? AtRule(int index)
    {
        if (_code[index] != '@' || index + 1 >= _end || !char.IsLetter(_code[index + 1]))
        {
            return null;
        }
        var ruleEnd = NameEnd(index + 1);
        _tokens.Add(index, ruleEnd, CodeTokenKind.Keyword);
        return ruleEnd;
    }

    private int? Important(int index)
    {
        const string Text = "!important";
        if (_code[index] != '!' || index + Text.Length > _end
            || string.Compare(_code, index, Text, 0, Text.Length, StringComparison.OrdinalIgnoreCase) != 0)
        {
            return null;
        }
        _tokens.Add(index, index + Text.Length, CodeTokenKind.Keyword);
        return index + Text.Length;
    }

    /// <summary>
    /// A rule nested in a block, as inside <c>@media</c> or with CSS nesting: what starts a declaration but reaches a
    /// <c>{</c> before a <c>;</c> or <c>}</c> is a selector, read up to that brace.
    /// </summary>
    private int? NestedRule(int index)
    {
        if (_depth == 0 || !_declarationStart || char.IsWhiteSpace(_code[index]))
        {
            return null;
        }
        var open = index;
        while (open < _end && _code[open] is not ('{' or ';' or '}'))
        {
            open++;
        }
        if (open >= _end || _code[open] != '{')
        {
            return null;
        }
        var position = index;
        while (position < open)
        {
            position = Selector(position, open);
        }
        _declarationStart = false;
        return position;
    }

    private int Declaration(int index)
    {
        var character = _code[index];
        if (_declarationStart && (char.IsLetter(character) || character == '-'))
        {
            _declarationStart = false;
            return PropertyName(index);
        }
        if (!char.IsWhiteSpace(character))
        {
            _declarationStart = false;
        }
        return NumberValue(index) ?? ColorValue(index) ?? (char.IsLetter(character) || character == '-' ? NameEnd(index) : index + 1);
    }

    /// <summary>A name at the start of a declaration, which is a property when a colon follows it.</summary>
    private int PropertyName(int index)
    {
        var nameEnd = NameEnd(index);
        var after = nameEnd;
        while (after < _end && _code[after] is ' ' or '\t')
        {
            after++;
        }
        if (after < _end && _code[after] == ':')
        {
            _tokens.Add(index, nameEnd, CodeTokenKind.Property);
        }
        return nameEnd;
    }

    /// <summary>A number with its unit, such as <c>4px</c>, <c>.5em</c> or <c>50%</c>.</summary>
    private int? NumberValue(int index)
    {
        var character = _code[index];
        if (!char.IsAsciiDigit(character) && !(character == '.' && index + 1 < _end && char.IsAsciiDigit(_code[index + 1])))
        {
            return null;
        }
        var numberEnd = index + 1;
        while (numberEnd < _end && (char.IsAsciiLetterOrDigit(_code[numberEnd]) || _code[numberEnd] is '.' or '%'))
        {
            numberEnd++;
        }
        _tokens.Add(index, numberEnd, CodeTokenKind.Constant);
        return numberEnd;
    }

    /// <summary>A hexadecimal color, such as <c>#fff</c>.</summary>
    private int? ColorValue(int index)
    {
        if (_code[index] != '#' || index + 1 >= _end || !char.IsAsciiHexDigit(_code[index + 1]))
        {
            return null;
        }
        var colorEnd = index + 1;
        while (colorEnd < _end && char.IsAsciiHexDigit(_code[colorEnd]))
        {
            colorEnd++;
        }
        _tokens.Add(index, colorEnd, CodeTokenKind.Constant);
        return colorEnd;
    }

    /// <summary>
    /// A selector name before <paramref name="limit"/>: an element, class or id, shown as a tag. Pseudo-classes stay plain.
    /// </summary>
    private int Selector(int index, int limit)
    {
        var character = _code[index];
        if (character == ':')
        {
            var pseudoEnd = index + 1;
            while (pseudoEnd < limit && _code[pseudoEnd] == ':')
            {
                pseudoEnd++;
            }
            return Math.Max(Math.Min(NameEnd(pseudoEnd), limit), index + 1);
        }
        var isName = char.IsLetter(character) || (character == '-' && index + 1 < limit && char.IsLetter(_code[index + 1]));
        if (!isName && character is not ('.' or '#' or '*'))
        {
            return index + 1;
        }
        var nameEnd = Math.Min(isName ? NameEnd(index) : NameEnd(index + 1), limit);
        nameEnd = Math.Max(nameEnd, index + 1);
        _tokens.Add(index, nameEnd, CodeTokenKind.Tag);
        return nameEnd;
    }

    private int StringEnd(int index)
    {
        var quote = _code[index];
        var position = index + 1;
        while (position < _end)
        {
            var character = _code[position];
            if (character == '\\')
            {
                position += 2;
            }
            else if (character == quote)
            {
                return position + 1;
            }
            else if (character == '\n')
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

    private int NameEnd(int index)
    {
        var position = index;
        while (position < _end && (char.IsLetterOrDigit(_code[position]) || _code[position] is '-' or '_'))
        {
            position++;
        }
        return position;
    }
}
