namespace Polhem.Markdown.Html;

/// <summary>What an <see cref="HtmlToken"/> is.</summary>
internal enum HtmlTokenKind
{
    /// <summary>Text between tags, with its character references decoded.</summary>
    Text,

    /// <summary>A start tag, such as <c>&lt;img src="x"&gt;</c>.</summary>
    StartTag,

    /// <summary>An end tag, such as <c>&lt;/b&gt;</c>.</summary>
    EndTag,
}
