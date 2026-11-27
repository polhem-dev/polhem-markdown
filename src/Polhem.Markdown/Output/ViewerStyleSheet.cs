using System.Text;

namespace Polhem.Markdown.Output;

/// <summary>
/// The stylesheet of the HTML that <see cref="MarkdownHtml"/> writes: the Blazor viewer's own file, which this assembly
/// embeds at build time (see Polhem.Markdown.csproj), less the rules of the parts a reader interacts with.
/// </summary>
/// <remarks>
/// NOTE: the file is shared rather than copied so that the exported HTML and the viewer cannot drift apart; a rule
/// added there reaches the HTML with the next build. The rules are flat, with no at-rules, which is what lets
/// <see cref="Filter"/> read them one by one; <c>StyleSheet_KeepsTheViewerRules</c> fails if that stops being so.
/// </remarks>
internal static class ViewerStyleSheet
{
    private const string ResourceName = "Polhem.Markdown.Output.polhem-markdown.css";

    // The rules of parts the exported HTML never has: the editor, the reader, the full-size image dialog, the matches of
    // a search, the button that copies code, and the button that opens an image.
    private static readonly string[] s_droppedPrefixes = [".pmd-editor", ".pmd-zoom", ".pmd-reader", "::highlight"];
    private static readonly string[] s_droppedClasses = [".pmd-copy", ".pmd-image-button"];

    private static readonly Lazy<string> s_content = new(() => Filter(Read()));

    /// <summary>The viewer's rules.</summary>
    public static string Content => s_content.Value;

    /// <summary>
    /// The rules of a standalone page: the system font, the text in a centred column, light or dark colors on screen and
    /// light ones on paper, and code and tables that wrap rather than scroll when printed.
    /// </summary>
    public const string Page = """
        html { color-scheme: light dark; }
        body { margin: 0; background: Canvas; color: CanvasText; line-height: 1.5;
          font-family: system-ui, -apple-system, "Segoe UI", Roboto, "Noto Sans", "Helvetica Neue", Arial, sans-serif; }
        body > .pmd { max-width: 860px; margin: 0 auto; padding: 32px 24px; }
        @media print {
          html { color-scheme: light; }
          body > .pmd { max-width: none; padding: 0; }
          .pmd .pmd-code pre code { white-space: pre-wrap; }
          .pmd :where(.pmd-code, .pmd-diagram, .pmd-math, tr, img) { break-inside: avoid; }
        }

        """;

    private static string Read()
    {
        using var stream = typeof(ViewerStyleSheet).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("The viewer stylesheet is not embedded in the assembly.");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>Drops the comments and the rules whose selector belongs to a part the HTML does not have.</summary>
    internal static string Filter(string css)
    {
        var result = new StringBuilder();
        var position = 0;
        var text = WithoutComments(css);
        while (position < text.Length)
        {
            var open = text.IndexOf('{', position);
            if (open < 0)
            {
                break;
            }
            var close = text.IndexOf('}', open);
            if (close < 0)
            {
                break;
            }
            var selector = text[position..open].Trim();
            if (!IsDropped(selector))
            {
                result.Append(selector).Append(" {").Append(text[(open + 1)..close].TrimEnd()).Append("\n}\n");
            }
            position = close + 1;
        }
        return result.ToString();
    }

    private static bool IsDropped(string selector) =>
        s_droppedPrefixes.Any(prefix => selector.StartsWith(prefix, StringComparison.Ordinal))
        || s_droppedClasses.Any(name => selector.Contains(name, StringComparison.Ordinal));

    private static string WithoutComments(string css)
    {
        var result = new StringBuilder(css.Length);
        var position = 0;
        while (position < css.Length)
        {
            var start = css.IndexOf("/*", position, StringComparison.Ordinal);
            if (start < 0)
            {
                result.Append(css, position, css.Length - position);
                break;
            }
            result.Append(css, position, start - position);
            var end = css.IndexOf("*/", start + 2, StringComparison.Ordinal);
            position = end < 0 ? css.Length : end + 2;
        }
        return result.ToString();
    }
}
