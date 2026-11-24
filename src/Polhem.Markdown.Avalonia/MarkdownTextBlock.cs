using Avalonia.Controls;
using Avalonia.Input;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A paragraph, heading or table cell: selectable text whose link runs can be clicked. A link either opens a URI or
/// moves to an anchor of the same text: a heading, a footnote, or a reference to one.
/// </summary>
/// <remarks>
/// NOTE: links are runs inside the text rather than controls placed in it, so a long link wraps like any other text and
/// a selection can run through it. A click is mapped to a character with the text layout and from there to a link.
/// </remarks>
internal sealed class MarkdownTextBlock : SelectableTextBlock
{
    private readonly MarkdownViewer _viewer;
    private readonly List<LinkRange> _links = [];
    private readonly List<string> _anchorIds = [];

    public MarkdownTextBlock(MarkdownViewer viewer)
    {
        _viewer = viewer;
    }

    /// <summary>
    /// Makes room below each line for a formula reaching <paramref name="depth"/> below its baseline, deeper than the
    /// text's own descenders. The text engine makes a line taller above an embedded control only, so the formula's
    /// lower part is drawn outside its line; the extra line spacing keeps it off the next line, and the padding off the
    /// next block.
    /// </summary>
    public void ReserveDepth(double depth)
    {
        var extra = depth - FontSize * TextDescent;
        if (extra <= 0)
        {
            return;
        }
        ClipToBounds = false;
        if (extra > LineSpacing)
        {
            LineSpacing = extra;
        }
        if (extra > Padding.Bottom)
        {
            Padding = new global::Avalonia.Thickness(Padding.Left, Padding.Top, Padding.Right, extra);
        }
    }

    /// <summary>How far a line's own descenders reach below its baseline, as a fraction of the font size.</summary>
    private const double TextDescent = 0.22;

    /// <summary>The links of the text, by character range.</summary>
    public IReadOnlyList<LinkRange> Links => _links;

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    public void AddLink(int start, int length, Uri target) => _links.Add(new LinkRange(start, length, target, null));

    /// <summary>Records an id a link in the text can move to this text by.</summary>
    public void AddAnchor(string id)
    {
        // The list is set once and then grown in place, so a paragraph with many footnote references does not copy it
        // for each one. Nothing observes the property's changes; it is only read when a link is followed.
        if (_anchorIds.Count == 0)
        {
            SetValue(MarkdownViewer.AnchorIdsProperty, _anchorIds);
        }
        _anchorIds.Add(id);
    }

    public void AddAnchorLink(int start, int length, string anchorId) => _links.Add(new LinkRange(start, length, null, anchorId));

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
        if (link.AnchorId is { } anchorId)
        {
            _viewer.ScrollToAnchor(anchorId);
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

    /// <summary>A link run: <see cref="Target"/> for a link that opens a URI, <see cref="AnchorId"/> for one within the text.</summary>
    internal sealed record LinkRange(int Start, int Length, Uri? Target, string? AnchorId);
}
