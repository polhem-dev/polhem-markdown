namespace Polhem.Markdown.Highlighting;

/// <summary>The languages <see cref="CodeScanner"/> reads, as configurations.</summary>
internal static class CodeLanguages
{
    public static CodeLanguage CSharp { get; } = new()
    {
        LineComments = ["//"],
        BlockComments = [("/*", "*/")],
        LongQuotes = ["\"\"\""],
        StringPrefixes = ["$"],
        VerbatimPrefixes = ["@$", "$@", "@"],
        Keywords = Words(
            "abstract as async await base bool break byte case catch char checked class const continue decimal default "
            + "delegate do double else enum event explicit extern finally fixed float for foreach get goto if implicit in "
            + "init int interface internal is lock long namespace new nint nuint object operator out override params "
            + "partial private protected public readonly record ref required return sbyte sealed set short sizeof "
            + "stackalloc static string struct switch this throw try typeof uint ulong unchecked unsafe ushort using var "
            + "virtual void volatile when where while with yield"),
        Constants = Words("true false null"),
    };

    public static CodeLanguage Json { get; } = new()
    {
        LineComments = ["//"],
        BlockComments = [("/*", "*/")],
        Quotes = "\"",
        Constants = Words("true false null"),
        KeysBeforeColon = true,
    };

    public static CodeLanguage Sql { get; } = new()
    {
        LineComments = ["--"],
        BlockComments = [("/*", "*/")],
        Quotes = "'",
        MultilineQuotes = "'",
        Escape = '\0',
        IgnoreCase = true,
        Keywords = WordsIgnoringCase(
            "add all alter and any as asc begin between by case check column commit constraint create cross database "
            + "default delete desc distinct drop else end exec execute exists foreign from full group having if in index "
            + "inner insert into is join key left like limit merge not of offset on or order outer primary procedure "
            + "references returning right rollback select set table then top transaction trigger truncate union unique "
            + "update using values view when where while with "
            + "bigint bit char date datetime datetime2 decimal float int integer nchar numeric nvarchar real smallint text "
            + "time timestamp varchar"),
        Constants = WordsIgnoringCase("null true false"),
    };

    public static CodeLanguage Shell { get; } = new()
    {
        LineComments = ["#"],
        MultilineQuotes = "\"'",
        VariablePrefix = '$',
        Keywords = Words(
            "if then else elif fi for while until do done case esac in function return local export readonly declare "
            + "unset shift break continue exit select time"),
        Constants = Words("true false"),
    };

    public static CodeLanguage PowerShell { get; } = new()
    {
        LineComments = ["#"],
        BlockComments = [("<#", "#>")],
        MultilineQuotes = "\"'",
        Escape = '`',
        IgnoreCase = true,
        VariablePrefix = '$',
        NameCharacters = "-",
        Keywords = WordsIgnoringCase(
            "begin break catch class continue data default do dynamicparam else elseif end enum exit filter finally for "
            + "foreach function if in param process return switch throw trap try until using while "
            + "-and -or -not -xor -eq -ne -gt -ge -lt -le -like -notlike -match -notmatch -contains -notcontains -in "
            + "-notin -replace -is -isnot -as"),
        Constants = WordsIgnoringCase("$true $false $null"),
    };

    public static CodeLanguage JavaScript { get; } = new()
    {
        LineComments = ["//"],
        BlockComments = [("/*", "*/")],
        Quotes = "\"'`",
        MultilineQuotes = "`",
        NameCharacters = "$",
        Keywords = Words(
            "abstract any as async await boolean break case catch class const constructor continue debugger declare "
            + "default delete do else enum export extends finally for from function get if implements import in "
            + "instanceof interface keyof let module namespace never new number of package private protected public "
            + "readonly return satisfies set static string super switch symbol this throw try type typeof unknown var "
            + "void while with yield"),
        Constants = Words("true false null undefined NaN Infinity"),
    };

    public static CodeLanguage Python { get; } = new()
    {
        LineComments = ["#"],
        LongQuotes = ["\"\"\"", "'''"],
        StringPrefixes = ["f", "b", "u"],
        VerbatimPrefixes = ["rb", "br", "rf", "fr", "r"],
        Keywords = Words(
            "and as assert async await break class continue def del elif else except finally for from global if import "
            + "in is lambda match nonlocal not or pass raise return try while with yield case"),
        Constants = Words("True False None"),
    };

    private static HashSet<string> Words(string words) =>
        new(words.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);

    // Built with the comparer the scanner uses for a case-insensitive language, so it is not copied per block.
    private static HashSet<string> WordsIgnoringCase(string words) =>
        new(words.Split(' ', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);
}
