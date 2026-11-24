namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// Which SVG images the Avalonia viewer draws: those served over <c>https</c> by a trusted badge host, or GitHub's
/// workflow badges. Any other SVG shows its alternative text. See maintainers/adr/adr-001-svg-from-trusted-hosts.md.
/// </summary>
internal static class SvgTrust
{
    /// <summary>
    /// The hosts trusted by default: the list VS Code's packaging tool accepts SVG badges from (<c>TrustedSVGSources</c>
    /// in vscode-vsce's <c>src/package.ts</c>), taken as it stood on 2026-11-24.
    /// </summary>
    public static IReadOnlyList<string> DefaultHosts { get; } =
    [
        "api.travis-ci.com", "app.fossa.io", "badge.buildkite.com", "badge.fury.io", "badgen.net", "badges.frapsoft.com",
        "badges.gitter.im", "cdn.travis-ci.com", "ci.appveyor.com", "circleci.com", "cla.opensource.microsoft.com",
        "codacy.com", "codeclimate.com", "codecov.io", "coveralls.io", "david-dm.org", "deepscan.io", "dev.azure.com",
        "docs.rs", "flat.badgen.net", "gitlab.com", "godoc.org", "goreportcard.com", "img.shields.io",
        "isitmaintained.com", "marketplace.visualstudio.com", "nodesecurity.io", "opencollective.com", "snyk.io",
        "travis-ci.com", "visualstudio.com", "vsmarketplacebadges.dev",
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
