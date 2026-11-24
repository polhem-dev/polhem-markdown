namespace Polhem.Markdown.Html;

/// <summary>
/// The HTML elements the model takes over from raw HTML, by what they become. Any other HTML element is dropped and its
/// content kept, except the ones in <see cref="IsDiscarded"/>, whose content is dropped too. A tag whose name is no HTML
/// element shows as written (see <see cref="IsElement"/>).
/// </summary>
internal static class HtmlElements
{
    // NOTE: the elements of the HTML standard, with the obsolete ones README files still use. Prose about code writes
    // `List<string>` or `Task<T>` without backticks, which CommonMark reads as tags; such names are none of these, so the
    // text shows as written instead of losing the type argument.
    private static readonly HashSet<string> s_elements = new(StringComparer.Ordinal)
    {
        "a", "abbr", "acronym", "address", "area", "article", "aside", "audio", "b", "base", "bdi", "bdo", "big",
        "blockquote", "body", "br", "button", "canvas", "caption", "center", "cite", "code", "col", "colgroup", "data",
        "datalist", "dd", "del", "details", "dfn", "dialog", "dir", "div", "dl", "dt", "em", "embed", "fieldset",
        "figcaption", "figure", "font", "footer", "form", "frame", "frameset", "h1", "h2", "h3", "h4", "h5", "h6", "head",
        "header", "hgroup", "hr", "html", "i", "iframe", "img", "input", "ins", "kbd", "label", "legend", "li", "link",
        "main", "map", "mark", "marquee", "menu", "meta", "meter", "nav", "nobr", "noembed", "noframes", "noscript",
        "object", "ol", "optgroup", "option", "output", "p", "param", "picture", "pre", "progress", "q", "rp", "rt",
        "ruby", "s", "samp", "script", "search", "section", "select", "slot", "small", "source", "span", "strike",
        "strong", "style", "sub", "summary", "sup", "svg", "math", "table", "tbody", "td", "template", "textarea",
        "tfoot", "th", "thead", "time", "title", "tr", "track", "tt", "u", "ul", "var", "video", "wbr", "xmp",
    };

    /// <summary>Whether the name is an HTML element's; a tag with any other name is text that only looks like a tag.</summary>
    public static bool IsElement(string name) => s_elements.Contains(name);

    /// <summary>Whether the element's content is dropped with it: scripts, styles, and content that is not shown as text.</summary>
    public static bool IsDiscarded(string name) => name is "script" or "style" or "textarea" or "title" or "iframe" or "noscript" or "template";

    /// <summary>Whether the element sets its text apart within a line: emphasis, code, and links.</summary>
    public static bool IsInline(string name) =>
        name is "b" or "strong" or "i" or "em" or "sup" or "sub" or "kbd" or "code" or "a" or "picture";

    /// <summary>Whether the element groups blocks: a paragraph, a division, or a disclosure and its summary.</summary>
    public static bool IsBlock(string name) => name is "p" or "div" or "details" or "summary";
}
