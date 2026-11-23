using Avalonia.Controls;
using Avalonia.Input;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A paragraph, heading or table cell: selectable text whose link runs can be clicked. A link either opens a URI or
/// moves to a heading of the same text.
/// </summary>
/// <remarks>
/// NOTE: links are runs inside the text rather than controls placed in it, so a long link wraps like any other text and
/// a selection can run through it. A click is mapped to a character with the text layout and from there to a link.
/// </remarks>
internal sealed class MarkdownTextBlock : SelectableTextBlock
{
    private readonly MarkdownViewer _viewer;
    private readonly List<LinkRange> _links = [];

    public MarkdownTextBlock(MarkdownViewer viewer)
    {
        _viewer = viewer;
    }

    /// <summary>The links of the text, by character range.</summary>
    public IReadOnlyList<LinkRange> Links => _links;

    /// <summary>The id an in-document link refers to this text by, when it is a heading; otherwise empty.</summary>
    public string HeadingId { get; set; } = "";

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    public void AddLink(int start, int length, Uri target) => _links.Add(new LinkRange(start, length, target, null));

    public void AddAnchorLink(int start, int length, string headingId) => _links.Add(new LinkRange(start, length, null, headingId));

    /// <summary>The target of the link at a character index of the text, or <see langword="null"/>.</summary>
    public Uri? LinkAt(int index) => LinkRangeAt(index)?.Target;

    /// <summary>The link at a character index of the text, or <see langword="null"/>.</summary>
    public LinkRange? LinkRangeAt(int index) =>
        _links.FirstOrDefault(link => index >= link.Start && index < link.Start + link.Length);

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_links.Count > 0)
        {
            Cursor = LinkUnder(e) is null ? null : new Cursor(StandardCursorType.Hand);
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton != MouseButton.Left || SelectionStart != SelectionEnd)
        {
            return;
        }
        if (LinkUnder(e) is not { } link)
        {
            return;
        }
        if (link.HeadingId is { } headingId)
        {
            _viewer.ScrollToHeading(headingId);
        }
        else if (link.Target is { } target)
        {
            _viewer.ActivateLink(this, target);
        }
    }

    private LinkRange? LinkUnder(PointerEventArgs e)
    {
        if (_links.Count == 0)
        {
            return null;
        }
        var point = e.GetPosition(this) - new global::Avalonia.Point(Padding.Left, Padding.Top);
        var hit = TextLayout.HitTestPoint(point);
        return hit.IsInside ? LinkRangeAt(hit.TextPosition) : null;
    }

    /// <summary>A link run: <see cref="Target"/> for a link that opens a URI, <see cref="HeadingId"/> for one to a heading.</summary>
    internal sealed record LinkRange(int Start, int Length, Uri? Target, string? HeadingId);
}
