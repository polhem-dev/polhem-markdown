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
    private static readonly HashSet<string> s_blockDirectives = new(StringComparer.Ordinal) { "code", "functions" };

    private static readonly HashSet<string> s_lineDirectives = new(StringComparer.Ordinal)
    {
        "page", "using", "inject", "model", "inherits", "implements", "layout", "namespace", "attribute", "rendermode",
        "typeparam", "preservewhitespace", "section",
    };

    private static readonly HashSet<string> s_controlKeywords = new(StringComparer.Ordinal)
    {
        "if", "else", "foreach", "for", "while", "do", "switch", "try", "catch", "finally", "lock", "await",
    };

    private RazorScanner(string code, TokenList tokens)
        : base(code, tokens)
    {
    }

    public static new void Scan(string code, TokenList tokens) => new RazorScanner(code, tokens).Run();

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
        var next = Code[index + 1];
        switch (next)
        {
            case '@':
                return index + 2;
            case '*':
                var close = Code.IndexOf("*@", index + 2, StringComparison.Ordinal);
                var commentEnd = close < 0 ? Code.Length : close + 2;
                Tokens.Add(index, commentEnd, CodeTokenKind.Comment);
                return commentEnd;
            case '{' or '(':
                Tokens.Add(index, index + 1, CodeTokenKind.Keyword);
                return CSharpGroup(index + 1);
        }
        var nameEnd = index + 1;
        while (nameEnd < Code.Length && (char.IsLetterOrDigit(Code[nameEnd]) || Code[nameEnd] == '_'))
        {
            nameEnd++;
        }
        var word = Code[(index + 1)..nameEnd];
        if (s_blockDirectives.Contains(word) || s_controlKeywords.Contains(word))
        {
            Tokens.Add(index, nameEnd, CodeTokenKind.Keyword);
            var after = nameEnd;
            while (after < Code.Length && Code[after] is ' ' or '\t' or '\r' or '\n')
            {
                after++;
            }
            if (after < Code.Length && (Code[after] == '(' || (Code[after] == '{' && s_blockDirectives.Contains(word))))
            {
                return CSharpGroup(after);
            }
            return nameEnd;
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
            expressionEnd++;
            while (expressionEnd < Code.Length && (char.IsLetterOrDigit(Code[expressionEnd]) || Code[expressionEnd] == '_'))
            {
                expressionEnd++;
            }
        }
        Tokens.Add(index, expressionEnd, CodeTokenKind.Property);
        return expressionEnd;
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
            var character = Code[position];
            if (character is '"' or '\'')
            {
                position = SkipQuoted(position);
                continue;
            }
            if (character == '/' && position + 1 < Code.Length && Code[position + 1] is '/' or '*')
            {
                position = Code[position + 1] == '/'
                    ? CodeScanner.LineEnd(Code, position, Code.Length)
                    : SkipPast(position + 2, "*/");
                continue;
            }
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
        var end = Math.Min(position + 1, Code.Length);
        CodeScanner.Scan(Code, open + 1, Math.Min(position, Code.Length), CodeLanguages.CSharp, Tokens);
        return end;
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
