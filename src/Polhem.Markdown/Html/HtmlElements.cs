namespace Polhem.Markdown.Html;

/// <summary>
/// The HTML elements the model takes over from raw HTML, by what they become. Any other element is dropped and its
/// content kept, except the ones in <see cref="IsDiscarded"/>, whose content is dropped too.
/// </summary>
internal static class HtmlElements
{
    /// <summary>Whether the element's content is dropped with it: scripts, styles, and content that is not shown as text.</summary>
    public static bool IsDiscarded(string name) => name is "script" or "style" or "textarea" or "title" or "iframe" or "noscript" or "template";

    /// <summary>Whether the element sets its text apart within a line: emphasis, code, and links.</summary>
    public static bool IsInline(string name) =>
        name is "b" or "strong" or "i" or "em" or "sup" or "sub" or "kbd" or "code" or "a" or "picture";

    /// <summary>Whether the element groups blocks: a paragraph, a division, or a disclosure and its summary.</summary>
    public static bool IsBlock(string name) => name is "p" or "div" or "details" or "summary";
}
