namespace Polhem.Markdown.Highlighting;

/// <summary>What a highlighted stretch of code is, which decides its color.</summary>
internal enum CodeTokenKind
{
    /// <summary>A keyword of the language.</summary>
    Keyword,

    /// <summary>A string or character literal.</summary>
    String,

    /// <summary>A comment.</summary>
    Comment,

    /// <summary>A number or a constant such as <c>true</c>, <c>false</c> or <c>null</c>.</summary>
    Constant,

    /// <summary>The name of a markup tag.</summary>
    Tag,

    /// <summary>An attribute name, a JSON or YAML key, a CSS property, or a shell variable.</summary>
    Property,
}
