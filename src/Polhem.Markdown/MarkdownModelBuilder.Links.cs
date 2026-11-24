using Markdig.Syntax.Inlines;
using Polhem.Markdown.Model;

namespace Polhem.Markdown;

internal sealed partial class MarkdownModelBuilder
{
    /// <summary>
    /// Adds a link to <paramref name="inlines"/>, or only its content when the target is refused. Content that is empty
    /// becomes the target's text.
    /// </summary>
    internal void AddLink(SourceRange range, string? target, List<InlineNode> content, List<InlineNode> inlines)
    {
        if (target is ['#', ..])
        {
            AddAnchorLink(range, target, content, inlines);
            return;
        }
        if (!TryCreateTarget(target, _options.AllowedLinkSchemes, out var uri))
        {
            inlines.AddRange(content);
            return;
        }
        if (content.Count == 0)
        {
            content.Add(new TextNode(range, target!));
        }
        inlines.Add(new LinkNode(range, uri, content));
    }

    /// <summary>
    /// Adds a link to a heading of this text, or only its content when no heading has the id. The id may be written
    /// percent-encoded, as GitHub writes a link to a heading with non-ASCII letters.
    /// </summary>
    private void AddAnchorLink(SourceRange range, string target, List<InlineNode> content, List<InlineNode> inlines)
    {
        var id = Uri.UnescapeDataString(target[1..]);
        if (!_ids.Contains(id))
        {
            inlines.AddRange(content);
            return;
        }
        if (content.Count == 0)
        {
            content.Add(new TextNode(range, target));
        }
        inlines.Add(new AnchorLinkNode(range, id, content));
    }

    private InlineNode Image(LinkInline image) =>
        Image(Range(image), image.GetDynamicUrl?.Invoke() ?? image.Url, PlainText(image), image.Title);

    /// <summary>An image whose source is allowed, or its alternative text.</summary>
    internal InlineNode Image(SourceRange range, string? source, string alternative, string? title)
    {
        if (ImageSource(source) is not { } uri)
        {
            return new TextNode(range, alternative);
        }
        return new ImageNode(range, uri, alternative, string.IsNullOrEmpty(title) ? null : title);
    }

    /// <summary>The URI of an image source the policy allows, or <see langword="null"/>.</summary>
    internal Uri? ImageSource(string? source) => TryCreateTarget(source, _options.AllowedImageSchemes, out var uri) ? uri : null;

    /// <summary>
    /// The URI a link goes to: its target when the target names its scheme, otherwise the target as a relative reference
    /// resolved against <see cref="MarkdownOptions.BaseUri"/> when one is set. Either way the scheme must be allowed.
    /// </summary>
    private bool TryCreateTarget(string? target, ISet<string> schemes, out Uri uri)
    {
        // WARNING: .NET reads a rooted path such as `/etc/passwd` (on Unix) or `C:\x` (on Windows) as an absolute
        // `file` URI. Only a target that writes its scheme counts as absolute here, so such a path is a relative
        // reference: with a web base it stays on the base's host, and without a base it is text, even when the host
        // allows `file`. The scheme check applies to both outcomes; do not relax it to "any absolute URI".
        if (Uri.TryCreate(target, UriKind.Absolute, out var created) && WritesScheme(target, created) && IsAllowed(created, schemes))
        {
            uri = created;
            return true;
        }
        if (target is not null && _options.BaseUri is { } baseUri
            && Uri.TryCreate(target, UriKind.Relative, out var relative)
            && Uri.TryCreate(baseUri, relative, out var resolved) && IsAllowed(resolved, schemes))
        {
            uri = resolved;
            return true;
        }
        uri = null!;
        return false;
    }

    // `Uri.TryCreate` trims surrounding white space, which an angle-bracket target such as `< https://x>` can keep.
    private static bool WritesScheme(string target, Uri uri)
    {
        var written = target.AsSpan().TrimStart();
        return written.Length > uri.Scheme.Length && written[uri.Scheme.Length] == ':'
            && written.StartsWith(uri.Scheme, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAllowed(Uri uri, ISet<string> schemes) => uri.IsAbsoluteUri && schemes.Contains(uri.Scheme);
}
