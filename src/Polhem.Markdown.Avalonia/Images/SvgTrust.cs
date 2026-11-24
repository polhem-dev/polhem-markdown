namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// Which SVG images the Avalonia viewer draws: those served over <c>https</c> by a trusted badge host, or GitHub's
/// workflow badges. Any other SVG shows its alternative text. See maintainers/adr/adr-001-svg-from-trusted-hosts.md.
/// </summary>
internal static class SvgTrust
{
    /// <summary>
    /// The hosts trusted by default: badge services that answer with SVG they generate themselves, serve no file a user
    /// uploaded from the same host, and answer directly rather than through a redirect. They were picked from VS Code's
    /// list of trusted badge hosts and checked on 2026-11-24; see ADR-001 for those left out and why.
    /// </summary>
    public static IReadOnlyList<string> DefaultHosts { get; } =
    [
        "img.shields.io", "badgen.net", "flat.badgen.net", "codecov.io", "vsmarketplacebadges.dev", "goreportcard.com",
        "circleci.com", "snyk.io",
    ];

    /// <summary>
    /// Whether an SVG at <paramref name="source"/> may be drawn: <c>https</c>, and a host in <paramref name="hosts"/> or
    /// a GitHub workflow badge (<c>https://github.com/{owner}/{repository}/actions/workflows/{file}/badge.svg</c>).
    /// </summary>
    public static bool IsTrusted(Uri source, IReadOnlySet<string> hosts) =>
        source.IsAbsoluteUri && source.Scheme == Uri.UriSchemeHttps && (hosts.Contains(source.Host) || IsGitHubWorkflowBadge(source));

    private static bool IsGitHubWorkflowBadge(Uri source)
    {
        if (!string.Equals(source.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        var segments = source.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var workflows = segments.Length > 2 && segments[2] == "actions" ? 3 : 2;
        return segments.Length > workflows + 1
            && segments[workflows] == "workflows"
            && segments[^1] == "badge.svg";
    }
}
