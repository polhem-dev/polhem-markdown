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
    /// A link with any other scheme is shown as its text and cannot be clicked, and so is a relative link unless
    /// <see cref="BaseUri"/> is set, in which case the scheme of the resolved link is checked. Add a scheme of the host's
    /// own (for example one that opens a form) to make such links clickable; the viewer then reports them to the host,
    /// which decides what they do.
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

    /// <summary>
    /// Gets or sets the absolute URI relative links resolve against, such as the address of the document being shown.
    /// The default is <see langword="null"/>, which leaves relative links as text.
    /// </summary>
    /// <remarks>
    /// A target such as <c>CHANGELOG.md</c>, <c>../README.md</c> or <c>/docs/setup.md</c> resolves by the standard URI
    /// rules, as in a browser, so a target starting with <c>/</c> resolves against the root of the base's host. The
    /// resolved link must still use a scheme in <see cref="AllowedLinkSchemes"/>: with a <c>file</c> base, add
    /// <c>file</c> to make the links clickable. A link to a heading of the same text (<c>#id</c>) is not affected.
    /// </remarks>
    /// <exception cref="ArgumentException">The value is not an absolute URI.</exception>
    public Uri? BaseUri
    {
        get => _baseUri;
        set
        {
            if (value is { IsAbsoluteUri: false })
            {
                throw new ArgumentException("The base URI must be absolute.", nameof(value));
            }
            _baseUri = value;
        }
    }

    private Uri? _baseUri;
}
