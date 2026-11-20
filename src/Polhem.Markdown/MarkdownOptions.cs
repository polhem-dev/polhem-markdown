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
}
