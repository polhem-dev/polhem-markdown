namespace Polhem.Markdown.Highlighting;

/// <summary>How <see cref="CodeScanner"/> reads one language.</summary>
internal sealed class CodeLanguage
{
    /// <summary>Prefixes that start a comment running to the end of the line.</summary>
    public IReadOnlyList<string> LineComments { get; init; } = [];

    /// <summary>Openers and closers of comments that can span lines.</summary>
    public IReadOnlyList<(string Open, string Close)> BlockComments { get; init; } = [];

    /// <summary>Characters that open and close a string, each closing the string it opened.</summary>
    public string Quotes { get; init; } = "\"'";

    /// <summary>Quote characters whose strings can span lines, such as JavaScript's backtick.</summary>
    public string MultilineQuotes { get; init; } = "";

    /// <summary>Openers of strings that also end at their own opener, such as Python's triple quotes.</summary>
    public IReadOnlyList<string> LongQuotes { get; init; } = [];

    /// <summary>Prefixes that belong to the quoted string they come before, such as C#'s <c>$</c> or Python's <c>f</c>.</summary>
    public IReadOnlyList<string> StringPrefixes { get; init; } = [];

    /// <summary>Prefixes that make the next quoted string verbatim, where a backslash escapes nothing.</summary>
    public IReadOnlyList<string> VerbatimPrefixes { get; init; } = [];

    /// <summary>The character that escapes the next one in a string, or <c>'\0'</c> for none.</summary>
    public char Escape { get; init; } = '\\';

    public IReadOnlySet<string> Keywords { get; init; } = new HashSet<string>();

    public IReadOnlySet<string> Constants { get; init; } = new HashSet<string>();

    /// <summary>Whether keywords and constants match regardless of case, as in SQL and PowerShell.</summary>
    public bool IgnoreCase { get; init; }

    /// <summary>A character that, before a name, makes it a variable shown as a property (<c>$</c> in shells).</summary>
    public char VariablePrefix { get; init; }

    /// <summary>Whether a string followed by a colon is a key, shown as a property (JSON).</summary>
    public bool KeysBeforeColon { get; init; }

    /// <summary>Characters, besides letters, digits and underscores, that can be part of a name.</summary>
    public string NameCharacters { get; init; } = "";

    /// <summary>The string prefixes, longest first, each with whether it makes the string verbatim.</summary>
    public IReadOnlyList<(string Prefix, bool Verbatim)> AllPrefixes => _allPrefixes ??= StringPrefixes
        .Select(prefix => (prefix, false))
        .Concat(VerbatimPrefixes.Select(prefix => (prefix, true)))
        .OrderByDescending(pair => pair.prefix.Length)
        .ToArray();

    /// <summary>The characters a string prefix can start with.</summary>
    public string PrefixStarts => _prefixStarts ??= string.Concat(AllPrefixes.Select(pair => pair.Prefix[0]).Distinct());

    private (string, bool)[]? _allPrefixes;
    private string? _prefixStarts;
}
