namespace Polhem.Markdown.Highlighting;

/// <summary>
/// Reads XML and HTML: comments, tags with their attributes, entities, and in HTML the code inside <c>script</c> and
/// <c>style</c> elements. In XML those are ordinary elements, such as XAML's <c>Style</c>. Razor builds on it through
/// <see cref="ExpressionHook"/>.
/// </summary>
/// <remarks>
/// NOTE: one forward pass, like <see cref="CodeScanner"/>. A tag, comment or attribute value that is not closed runs to
/// the end of the code.
/// </remarks>
internal class MarkupScanner
{
    private readonly bool _html;

    protected MarkupScanner(string code, TokenList tokens, bool html)
    {
        Code = code;
        Tokens = tokens;
        _html = html;
    }

    protected string Code { get; }

    protected TokenList Tokens { get; }

    /// <summary>Scans <paramref name="code"/> as HTML when <paramref name="html"/> is set, otherwise as XML.</summary>
    public static void Scan(string code, TokenList tokens, bool html) => new MarkupScanner(code, tokens, html).Run();

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
        var position = TagBody(nameEnd);

        var opened = Code[index + 1] != '/' && Code[position - 1] == '>' && Code[position - 2] != '/';
        return opened && _html ? EmbeddedCode(Code[nameStart..nameEnd], position) : position;
    }

    /// <summary>
    /// Reads a tag's attributes up to its <c>&gt;</c>, and returns the index after it; or the index of a <c>&lt;</c> that
    /// starts another tag first, or the end of the code.
    /// </summary>
    private int TagBody(int index)
    {
        var position = index;
        while (position < Code.Length)
        {
            var character = Code[position];
            if (character == '>')
            {
                return position + 1;
            }
            if (character == '<')
            {
                return position;
            }
            if (character is '"' or '\'')
            {
                position = AttributeValue(position);
            }
            else if (IsNameStart(character))
            {
                var attributeEnd = NameEnd(position);
                Tokens.Add(position, attributeEnd, CodeTokenKind.Property);
                position = attributeEnd;
            }
            else
            {
                var hooked = ExpressionHook(position);
                position = hooked != position ? hooked : position + 1;
            }
        }
        return position;
    }

    /// <summary>
    /// After an HTML <c>script</c> or <c>style</c> start tag, reads its body as JavaScript or CSS up to the closing tag,
    /// and returns the index of that tag. After any other tag it reads nothing.
    /// </summary>
    private int EmbeddedCode(string name, int index)
    {
        var isScript = string.Equals(name, "script", StringComparison.OrdinalIgnoreCase);
        if (!isScript && !string.Equals(name, "style", StringComparison.OrdinalIgnoreCase))
        {
            return index;
        }
        var close = Code.IndexOf("</" + name, index, StringComparison.OrdinalIgnoreCase);
        var bodyEnd = close < 0 ? Code.Length : close;
        if (isScript)
        {
            CodeScanner.Scan(Code, index, bodyEnd, CodeLanguages.JavaScript, Tokens);
        }
        else
        {
            CssScanner.Scan(Code, index, bodyEnd, Tokens);
        }
        return bodyEnd;
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
