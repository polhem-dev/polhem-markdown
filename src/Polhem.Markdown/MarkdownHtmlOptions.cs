namespace Polhem.Markdown;

/// <summary>
/// Settings of the HTML that <see cref="MarkdownHtml"/> writes, beyond the <see cref="MarkdownOptions"/> it shares with
/// the viewers.
/// </summary>
public sealed class MarkdownHtmlOptions
{
    /// <summary>
    /// Gets or sets a function that returns the bytes of an image, to write the image into the HTML as a <c>data:</c> URI
    /// so that a saved page does not depend on the files next to it. The default is <see langword="null"/>, which keeps
    /// every image as a link.
    /// </summary>
    /// <remarks>
    /// The function receives each image source that passed the safety policy (see
    /// <see cref="MarkdownOptions.AllowedImageSchemes"/>), the sources for a dark or a light theme included, and returns
    /// <see langword="null"/> to keep that image as a link. It is called once for each source, while the HTML is written;
    /// an exception it throws ends the call that writes the HTML. The package never reads a file or the network itself.
    /// </remarks>
    public Func<Uri, MarkdownImageContent?>? ImageContent { get; set; }

    /// <summary>
    /// Gets or sets the title of a page written by <see cref="MarkdownHtml.ToPage"/>. The default is
    /// <see langword="null"/>, which takes the text of the document's first heading, and writes no title when it has none.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the language of a page written by <see cref="MarkdownHtml.ToPage"/>, such as <c>en</c> or
    /// <c>zh-TW</c>, which a browser uses to pick fonts and to hyphenate. The default is <see langword="null"/>, which
    /// writes none.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets CSS that a page written by <see cref="MarkdownHtml.ToPage"/> includes after
    /// <see cref="MarkdownHtml.StyleSheet"/>, such as values for the <c>--pmd-*</c> variables on <c>.pmd</c>. The
    /// default is <see langword="null"/>.
    /// </summary>
    /// <remarks>
    /// The CSS is written into a <c>style</c> element as given, so it must come from the host, never from the document.
    /// <see cref="MarkdownHtml.ToPage"/> throws <see cref="ArgumentException"/> when it contains <c>&lt;/style</c>, which
    /// would end the element.
    /// </remarks>
    public string? StyleSheet { get; set; }
}
