using System.Net;
using System.Text;
using Polhem.Markdown.Output;

namespace Polhem.Markdown;

/// <summary>
/// Writes Markdown as HTML, from the same model and under the same safety policy as the viewers, with the markup the
/// Blazor viewer draws: highlighted code, Mermaid diagrams as inline SVG and math as MathML, all static, with no script.
/// </summary>
/// <remarks>
/// The policy is the viewers': raw HTML never passes through as markup (a few elements become their nodes, any other tag
/// is dropped and its content kept), and a link or an image whose scheme is not allowed by <see cref="MarkdownOptions"/>
/// shows as its text. Only the parts of the viewer a reader interacts with are left out, such as the button that copies a
/// code block. A link to a heading or a footnote is an ordinary <c>#id</c> link, and the headings and notes carry those
/// ids.
/// </remarks>
public static class MarkdownHtml
{
    /// <summary>
    /// Gets the stylesheet <see cref="ToPage"/> includes, which gives the HTML the viewers' look. A host that places a
    /// fragment from <see cref="ToFragment"/> in a page of its own can include it too.
    /// </summary>
    /// <remarks>
    /// It is the Blazor viewer's stylesheet without the rules of its interactive parts, so a host themes the HTML the
    /// same way, by setting the <c>--pmd-*</c> variables on <c>.pmd</c> or above it. The font and the text color come from
    /// the page.
    /// </remarks>
    public static string StyleSheet => ViewerStyleSheet.Content;

    /// <summary>
    /// Writes <paramref name="markdown"/> as an HTML fragment, one <c>div</c> with the class <c>pmd</c>, for a host to
    /// place in a page of its own.
    /// </summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <param name="options">
    /// The settings the viewers use, such as the allowed schemes; <see langword="null"/> for the defaults.
    /// </param>
    /// <param name="htmlOptions">The settings of the HTML; <see langword="null"/> for the defaults.</param>
    /// <returns>The HTML.</returns>
    /// <remarks>
    /// The fragment carries no style; include <see cref="StyleSheet"/> in the page. The ids of headings and footnotes are
    /// written as they are, so they can clash with ids the page already has.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    public static string ToFragment(string markdown, MarkdownOptions? options = null, MarkdownHtmlOptions? htmlOptions = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        var document = MarkdownModelBuilder.Build(markdown, options ?? new MarkdownOptions());
        var html = new StringBuilder();
        new HtmlWriter(html, htmlOptions ?? new MarkdownHtmlOptions()).Document(document);
        return html.ToString();
    }

    /// <summary>
    /// Writes <paramref name="markdown"/> as a standalone HTML page, with <see cref="StyleSheet"/> in it, to save as a
    /// file or to print.
    /// </summary>
    /// <param name="markdown">The Markdown text.</param>
    /// <param name="options">
    /// The settings the viewers use, such as the allowed schemes; <see langword="null"/> for the defaults.
    /// </param>
    /// <param name="htmlOptions">
    /// The settings of the HTML, such as the page's title and how images are written; <see langword="null"/> for the
    /// defaults.
    /// </param>
    /// <returns>The HTML of the page.</returns>
    /// <remarks>
    /// The page has the text in a centred column, the font of the system and its light or dark colors, and light colors
    /// when printed. <see cref="MarkdownHtmlOptions.StyleSheet"/> adds to the style.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="markdown"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><see cref="MarkdownHtmlOptions.StyleSheet"/> contains <c>&lt;/style</c>.</exception>
    public static string ToPage(string markdown, MarkdownOptions? options = null, MarkdownHtmlOptions? htmlOptions = null)
    {
        ArgumentNullException.ThrowIfNull(markdown);
        htmlOptions ??= new MarkdownHtmlOptions();
        if (htmlOptions.StyleSheet is { } extra && extra.Contains("</style", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The style sheet must not contain </style.", nameof(htmlOptions));
        }

        var document = MarkdownModelBuilder.Build(markdown, options ?? new MarkdownOptions());
        var html = new StringBuilder();
        html.Append("<!DOCTYPE html>\n<html");
        if (!string.IsNullOrWhiteSpace(htmlOptions.Language))
        {
            html.Append(" lang=\"").Append(WebUtility.HtmlEncode(htmlOptions.Language)).Append('"');
        }
        html.Append(">\n<head>\n<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        var title = htmlOptions.Title ?? (document.Headings.Count > 0 ? document.Headings[0].Text : null);
        if (!string.IsNullOrEmpty(title))
        {
            html.Append("<title>").Append(WebUtility.HtmlEncode(title)).Append("</title>\n");
        }
        html.Append("<style>\n").Append(ViewerStyleSheet.Content).Append(ViewerStyleSheet.Page);
        if (!string.IsNullOrEmpty(htmlOptions.StyleSheet))
        {
            html.Append(htmlOptions.StyleSheet).Append('\n');
        }
        html.Append("</style>\n</head>\n<body>\n");
        new HtmlWriter(html, htmlOptions).Document(document);
        html.Append("\n</body>\n</html>\n");
        return html.ToString();
    }
}
