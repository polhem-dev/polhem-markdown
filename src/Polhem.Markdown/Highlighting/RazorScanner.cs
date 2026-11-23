namespace Polhem.Markdown.Highlighting;

/// <summary>
/// Reads Razor: HTML with C# after <c>@</c>. Directives and control keywords are keywords, <c>@{ }</c>,
/// <c>@code { }</c>, <c>@( )</c> and the conditions of control keywords are read as C#, an implicit expression such as
/// <c>@Model.Name</c> is a property, and <c>@* *@</c> is a comment.
/// </summary>
/// <remarks>
/// NOTE: markup inside a C# block, such as the body of <c>@if { }</c>, stays markup only when the braces belong to the
/// markup around it; a whole <c>@{ }</c> or <c>@code { }</c> block is read as C#. Like the other scanners it reads the
/// code once: a brace or parenthesis that is never closed ends the block at the end of the code.
/// </remarks>
internal sealed class RazorScanner : MarkupScanner
{
    private static readonly HashSet<string> s_blockDirectives = ["code", "functions"];

    private static readonly HashSet<string> s_lineDirectives =
    [
        "page", "using", "inject", "model", "inherits", "implements", "layout", "namespace", "attribute", "rendermode",
        "typeparam", "preservewhitespace", "section",
    ];

    private static readonly HashSet<string> s_controlKeywords =
    [
        "if", "else", "foreach", "for", "while", "do", "switch", "try", "catch", "finally", "lock", "await",
    ];

    private RazorScanner(string code, TokenList tokens)
        : base(code, tokens, html: true)
    {
    }

    public static void Scan(string code, TokenList tokens) => new RazorScanner(code, tokens).Run();

    /// <summary>
    /// An at sign starts C# unless it follows a letter or digit, as in an e-mail address, or nothing that can follow it
    /// in Razor does.
    /// </summary>
    protected override bool StartsExpression(int index)
    {
        if (Code[index] != '@' || index + 1 >= Code.Length || (index > 0 && char.IsLetterOrDigit(Code[index - 1])))
        {
            return false;
        }
        var next = Code[index + 1];
        return next is '@' or '*' or '{' or '(' or '_' || char.IsLetter(next);
    }

    protected override int ExpressionHook(int index)
    {
        if (!StartsExpression(index))
        {
            return index;
        }
        switch (Code[index + 1])
        {
            case '@':
                return index + 2;
            case '*':
                var commentEnd = SkipPast(index + 2, "*@");
                Tokens.Add(index, commentEnd, CodeTokenKind.Comment);
                return commentEnd;
            case '{' or '(':
                Tokens.Add(index, index + 1, CodeTokenKind.Keyword);
                return CSharpGroup(index + 1);
            default:
                return Word(index);
        }
    }

    /// <summary>Reads <c>@</c> and the word after it: a keyword, a directive, or the start of an implicit expression.</summary>
    private int Word(int index)
    {
        var nameEnd = NameEnd(index + 1);
        var word = Code[(index + 1)..nameEnd];
        if (s_blockDirectives.Contains(word) || s_controlKeywords.Contains(word))
        {
            Tokens.Add(index, nameEnd, CodeTokenKind.Keyword);
            return KeywordGroup(nameEnd, allowsBraces: s_blockDirectives.Contains(word));
        }
        if (s_lineDirectives.Contains(word))
        {
            Tokens.Add(index, nameEnd, CodeTokenKind.Keyword);
            var lineEnd = CodeScanner.LineEnd(Code, nameEnd, Code.Length);
            CodeScanner.Scan(Code, nameEnd, lineEnd, CodeLanguages.CSharp, Tokens);
            return lineEnd;
        }

        // An implicit expression: a name and the members after it.
        var expressionEnd = nameEnd;
        while (expressionEnd + 1 < Code.Length && Code[expressionEnd] == '.' && (char.IsLetter(Code[expressionEnd + 1]) || Code[expressionEnd + 1] == '_'))
        {
            expressionEnd = NameEnd(expressionEnd + 1);
        }
        Tokens.Add(index, expressionEnd, CodeTokenKind.Property);
        return expressionEnd;
    }

    /// <summary>
    /// After a keyword, reads its parenthesized condition as C#, or for <c>@code</c> and <c>@functions</c> its braces;
    /// otherwise returns <paramref name="index"/>, and what follows stays markup.
    /// </summary>
    private int KeywordGroup(int index, bool allowsBraces)
    {
        var after = index;
        while (after < Code.Length && Code[after] is ' ' or '\t' or '\r' or '\n')
        {
            after++;
        }
        return after < Code.Length && (Code[after] == '(' || (Code[after] == '{' && allowsBraces)) ? CSharpGroup(after) : index;
    }

    /// <summary>
    /// Reads the brace or parenthesis group opening at <paramref name="open"/> as C#, and returns the index after its
    /// closing bracket. Brackets inside strings, characters and comments do not count.
    /// </summary>
    private int CSharpGroup(int open)
    {
        var opener = Code[open];
        var closer = opener == '{' ? '}' : ')';
        var depth = 0;
        var position = open;
        while (position < Code.Length)
        {
            if (SkipNonCode(position) is { } skipped)
            {
                position = skipped;
                continue;
            }
            var character = Code[position];
            if (character == opener)
            {
                depth++;
            }
            else if (character == closer && --depth == 0)
            {
                break;
            }
            position++;
        }
        CodeScanner.Scan(Code, open + 1, Math.Min(position, Code.Length), CodeLanguages.CSharp, Tokens);
        return Math.Min(position + 1, Code.Length);
    }

    /// <summary>The index after a string, character or comment starting at <paramref name="index"/>, or <see langword="null"/>.</summary>
    private int? SkipNonCode(int index)
    {
        var character = Code[index];
        if (character is '"' or '\'')
        {
            return SkipQuoted(index);
        }
        if (character != '/' || index + 1 >= Code.Length)
        {
            return null;
        }
        return Code[index + 1] switch
        {
            '/' => CodeScanner.LineEnd(Code, index, Code.Length),
            '*' => SkipPast(index + 2, "*/"),
            _ => null,
        };
    }

    private int NameEnd(int index)
    {
        var position = index;
        while (position < Code.Length && (char.IsLetterOrDigit(Code[position]) || Code[position] == '_'))
        {
            position++;
        }
        return position;
    }

    private int SkipQuoted(int index)
    {
        var quote = Code[index];
        var position = index + 1;
        while (position < Code.Length && Code[position] != quote && Code[position] != '\n')
        {
            position += Code[position] == '\\' ? 2 : 1;
        }
        return Math.Min(position + 1, Code.Length);
    }

    private int SkipPast(int index, string close)
    {
        var at = Code.IndexOf(close, Math.Min(index, Code.Length), StringComparison.Ordinal);
        return at < 0 ? Code.Length : at + close.Length;
    }
}
