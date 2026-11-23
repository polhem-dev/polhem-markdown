namespace Polhem.Markdown.Highlighting;

/// <summary>
/// Splits the code of a fenced block into highlighted stretches by the language its fence names. A language it does not
/// know, or none, gives no stretches, and the code shows as plain text, as on GitHub.
/// </summary>
internal static class CodeHighlighter
{
    /// <summary>
    /// The tokens of <paramref name="code"/> written in <paramref name="language"/>, in order and not overlapping, each
    /// within the code.
    /// </summary>
    public static IReadOnlyList<CodeToken> Tokenize(string language, string code)
    {
        var tokens = new TokenList();
        switch (Normalize(language))
        {
            case "cs" or "csharp" or "c#":
                CodeScanner.Scan(code, CodeLanguages.CSharp, tokens);
                break;
            case "json" or "jsonc" or "json5":
                CodeScanner.Scan(code, CodeLanguages.Json, tokens);
                break;
            case "sql" or "tsql" or "mysql" or "postgresql" or "postgres" or "plsql" or "sqlite":
                CodeScanner.Scan(code, CodeLanguages.Sql, tokens);
                break;
            case "sh" or "bash" or "shell" or "zsh" or "console" or "shellsession":
                CodeScanner.Scan(code, CodeLanguages.Shell, tokens);
                break;
            case "powershell" or "ps1" or "ps" or "pwsh":
                CodeScanner.Scan(code, CodeLanguages.PowerShell, tokens);
                break;
            case "js" or "javascript" or "jsx" or "mjs" or "ts" or "typescript" or "tsx":
                CodeScanner.Scan(code, CodeLanguages.JavaScript, tokens);
                break;
            case "py" or "python" or "python3":
                CodeScanner.Scan(code, CodeLanguages.Python, tokens);
                break;
            case "xml" or "html" or "htm" or "xhtml" or "xaml" or "axaml" or "svg" or "csproj" or "props" or "targets"
                or "config" or "resx" or "xsd" or "xslt" or "plist":
                MarkupScanner.Scan(code, tokens);
                break;
            case "razor" or "cshtml":
                RazorScanner.Scan(code, tokens);
                break;
            case "yaml" or "yml":
                YamlScanner.Scan(code, tokens);
                break;
            case "css" or "scss" or "less":
                CssScanner.Scan(code, tokens);
                break;
        }
        return tokens.Tokens;
    }

    /// <summary>
    /// The language name of a fence's info string: its first word, lower case. A fence may add attributes after it
    /// (<c>```cs title="x"</c>) or wrap it in braces (<c>```{.cs}</c>).
    /// </summary>
    private static string Normalize(string language)
    {
        var name = language.AsSpan().Trim().TrimStart('{').TrimStart('.');
        var space = name.IndexOfAny(' ', '\t', '}');
        if (space >= 0)
        {
            name = name[..space];
        }
        return name.ToString().ToLowerInvariant();
    }
}
