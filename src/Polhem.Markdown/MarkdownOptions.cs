namespace Polhem.Markdown;

/// <summary>
/// Settings shared by the Markdown viewers.
/// </summary>
public sealed class MarkdownOptions
{
    /// <summary>
    /// Gets the URI schemes a link may use, compared without regard to case. The default is <c>http</c>, <c>https</c>
    /// and <c>mailto</c>.
    /// </summary>
    /// <remarks>
    /// A link with any other scheme, or with a target that is not an absolute URI, is shown as its text and cannot be
    /// clicked. Add a scheme of the host's own (for example one that opens a form) to make such links clickable; the
    /// viewer then reports them to the host, which decides what they do.
    /// </remarks>
    public ISet<string> AllowedLinkSchemes { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "http",
        "https",
        "mailto",
    };

    /// <summary>
    /// Gets or sets whether <c>mermaid</c> code blocks are drawn as diagrams. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// Flowcharts and sequence diagrams are drawn by the viewer itself; no script runs. A diagram of another type, or one
    /// that uses syntax the viewers do not support, shows as its code, as every block does when this is
    /// <see langword="false"/>.
    /// </remarks>
    public bool RenderMermaidDiagrams { get; set; } = true;
}
