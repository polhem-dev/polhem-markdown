namespace Polhem.Markdown.Highlighting;

/// <summary>
/// Reads XML and HTML: comments, tags with their attributes, entities, and in HTML the code inside <c>script</c> and
/// <c>style</c> elements. Razor builds on it through <see cref="ExpressionHook"/>.
/// </summary>
/// <remarks>
/// NOTE: one forward pass, like <see cref="CodeScanner"/>. A tag, comment or attribute value that is not closed runs to
/// the end of the code.
/// </remarks>
internal class MarkupScanner
{
    protected MarkupScanner(string code, TokenList tokens)
    {
        Code = code;
        Tokens = tokens;
    }

    protected string Code { get; }

    protected TokenList Tokens { get; }

    public static void Scan(string code, TokenList tokens) => new MarkupScanner(code, tokens).Run();

    protected void Run()
    {
        var index = 0;
        while (index < Code.Length)
        {
            index = Text(index);
        }
    }

    /// <summary>
    /// Lets a subclass read what starts at <paramref name="index"/> in text or an attribute value; returns the index after
    /// it, or <paramref name="index"/> when it read nothing.
    /// </summary>
    protected virtual int ExpressionHook(int index) => index;

    /// <summary>Whether <see cref="ExpressionHook"/> reads something at <paramref name="index"/>.</summary>
    protected virtual bool StartsExpression(int index) => false;

    private int Text(int index)
    {
        var hooked = ExpressionHook(index);
        if (hooked != index)
        {
            return hooked;
        }

        if (StartsWith(index, "<!--"))
        {
            return Until(index, "-->", CodeTokenKind.Comment);
        }
        if (StartsWith(index, "<![CDATA["))
        {
            return Until(index, "]]>", CodeTokenKind.String);
        }
        if (Code[index] == '<' && index + 1 < Code.Length && (IsNameStart(Code[index + 1]) || Code[index + 1] is '/' or '?' or '!'))
        {
            return Tag(index);
        }
        if (Code[index] == '&')
        {
            var semicolon = index + 1;
            while (semicolon < Code.Length && semicolon - index <= 32 && (char.IsAsciiLetterOrDigit(Code[semicolon]) || Code[semicolon] == '#'))
            {
                semicolon++;
            }
            if (semicolon < Code.Length && Code[semicolon] == ';' && semicolon > index + 1)
            {
                Tokens.Add(index, semicolon + 1, CodeTokenKind.Constant);
                return semicolon + 1;
            }
        }
        return index + 1;
    }

    private int Tag(int index)
    {
        var nameStart = index + 1;
        if (Code[nameStart] is '/' or '?' or '!')
        {
            nameStart++;
        }
        var nameEnd = NameEnd(nameStart);
        Tokens.Add(nameStart, nameEnd, CodeTokenKind.Tag);
        var name = Code[nameStart..nameEnd];

        var position = nameEnd;
        while (position < Code.Length)
        {
            var character = Code[position];
            if (character == '>')
            {
                position++;
                break;
            }
            if (character == '<')
            {
                return position;
            }
            if (character is '"' or '\'')
            {
                position = AttributeValue(position);
                continue;
            }
            if (IsNameStart(character))
            {
                var attributeEnd = NameEnd(position);
                Tokens.Add(position, attributeEnd, CodeTokenKind.Property);
                position = attributeEnd;
                continue;
            }
            var hooked = ExpressionHook(position);
            position = hooked != position ? hooked : position + 1;
        }

        // The body of a script or style element is code in another language, up to its closing tag.
        var opened = Code[index + 1] != '/' && Code[position - 1] == '>' && Code[position - 2] != '/';
        var isScript = string.Equals(name, "script", StringComparison.OrdinalIgnoreCase);
        var isStyle = string.Equals(name, "style", StringComparison.OrdinalIgnoreCase);
        if (opened && (isScript || isStyle))
        {
            var close = Code.IndexOf("</" + name, position, StringComparison.OrdinalIgnoreCase);
            var bodyEnd = close < 0 ? Code.Length : close;
            if (isScript)
            {
                CodeScanner.Scan(Code, position, bodyEnd, CodeLanguages.JavaScript, Tokens);
            }
            else
            {
                CssScanner.Scan(Code, position, bodyEnd, Tokens);
            }
            return bodyEnd;
        }
        return position;
    }

    private int AttributeValue(int index)
    {
        var quote = Code[index];
        var position = index + 1;
        var valueStart = index;
        while (position < Code.Length && Code[position] != quote)
        {
            if (StartsExpression(position))
            {
                // The value so far is added first, so the tokens stay in order.
                Tokens.Add(valueStart, position, CodeTokenKind.String);
                position = ExpressionHook(position);
                valueStart = position;
                continue;
            }
            position++;
        }
        var end = position < Code.Length ? position + 1 : position;
        Tokens.Add(valueStart, end, CodeTokenKind.String);
        return end;
    }

    private int Until(int index, string close, CodeTokenKind kind)
    {
        var closeAt = Code.IndexOf(close, index + 1, StringComparison.Ordinal);
        var end = closeAt < 0 ? Code.Length : closeAt + close.Length;
        Tokens.Add(index, end, kind);
        return end;
    }

    protected bool StartsWith(int index, string text) =>
        text.Length <= Code.Length - index && string.CompareOrdinal(Code, index, text, 0, text.Length) == 0;

    private static bool IsNameStart(char character) => char.IsLetter(character) || character is '_' or ':';

    private int NameEnd(int index)
    {
        var position = index;
        while (position < Code.Length && (char.IsLetterOrDigit(Code[position]) || Code[position] is '_' or ':' or '.' or '-'))
        {
            position++;
        }
        return position;
    }
}
