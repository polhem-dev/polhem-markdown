using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A paragraph, heading, table cell or the code of a code block: selectable text whose link runs can be clicked. A link either opens a URI or
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
    private readonly Dictionary<int, string> _embeddedTexts = [];
    private string? _layoutText;

    public MarkdownTextBlock(MarkdownViewer viewer)
    {
        _viewer = viewer;

        // A text block without a background takes the pointer only where it draws glyphs. With one, a drag can start in
        // the space after a short line, as it can in a browser.
        Background = global::Avalonia.Media.Brushes.Transparent;
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

    /// <summary>
    /// Records the text an embedded control at <paramref name="offset"/> is copied as: an image's alternative text, or a
    /// formula's LaTeX.
    /// </summary>
    public void AddEmbeddedText(int offset, string text) => _embeddedTexts[offset] = text;

    /// <summary>
    /// The selected text as it is copied, with an image as its alternative text and a formula as its LaTeX.
    /// </summary>
    /// <remarks>
    /// NOTE: this does not use <see cref="SelectableTextBlock.SelectedText"/>. Up to Avalonia 12.0, an embedded control
    /// takes one position in the layout, which the selection counts in, but no character in
    /// <see cref="InlineCollection.Text"/>, so the selected text after an image or a formula was shifted by one character
    /// for each; from 12.1 it is an object replacement character. The text built here follows the layout on both.
    /// </remarks>
    public string CopiedText
    {
        get
        {
            var text = LayoutText;
            var start = Math.Clamp(Math.Min(SelectionStart, SelectionEnd), 0, text.Length);
            var end = Math.Clamp(Math.Max(SelectionStart, SelectionEnd), 0, text.Length);
            if (start == end)
            {
                return "";
            }
            var builder = new System.Text.StringBuilder(end - start);
            for (var index = start; index < end; index++)
            {
                if (text[index] != ObjectReplacement)
                {
                    builder.Append(text[index]);
                }
                else if (_embeddedTexts.TryGetValue(index, out var embedded))
                {
                    builder.Append(embedded);
                }
            }
            return builder.ToString();
        }
    }

    /// <summary>The number of positions in the text's layout, which is where a selection can end.</summary>
    public int LayoutLength => LayoutText.Length;

    /// <summary>The text as its layout counts it: an object replacement character for each embedded control.</summary>
    private string LayoutText => _layoutText ??= Inlines is { Count: > 0 } inlines ? Flatten(inlines) : Text ?? "";

    private static string Flatten(InlineCollection inlines)
    {
        var builder = new System.Text.StringBuilder();
        Append(inlines, builder);
        return builder.ToString();

        static void Append(InlineCollection inlines, System.Text.StringBuilder builder)
        {
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case InlineUIContainer:
                        builder.Append(ObjectReplacement);
                        break;
                    case LineBreak:
                        builder.Append('\n');
                        break;
                    case Span span:
                        Append(span.Inlines, builder);
                        break;
                    case Run run:
                        builder.Append(run.Text);
                        break;
                }
            }
        }
    }

    private const char ObjectReplacement = '\uFFFC';

    public void AddAnchorLink(int start, int length, string anchorId) => _links.Add(new LinkRange(start, length, null, anchorId));

    /// <summary>The target of the link at a character index of the text, or <see langword="null"/>.</summary>
    public Uri? LinkAt(int index) => LinkRangeAt(index)?.Target;

    /// <summary>The link at a character index of the text, or <see langword="null"/>.</summary>
    public LinkRange? LinkRangeAt(int index) =>
        _links.FirstOrDefault(link => index >= link.Start && index < link.Start + link.Length);

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        // Avalonia clears a text's selection when the text loses the focus. Within a selection that spans texts, the
        // viewer decides instead (see `MarkdownViewer.KeepsSelectionOnLostFocus`).
        var (start, end) = (SelectionStart, SelectionEnd);
        base.OnLostFocus(e);
        if (_viewer.KeepsSelectionOnLostFocus(this, e.NewFocusedElement) && start != end)
        {
            SetCurrentValue(SelectionStartProperty, start);
            SetCurrentValue(SelectionEndProperty, end);
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        // NOTE: a triple click selects the whole text with `SelectAll`, which up to Avalonia 12.0 stops short by one
        // position for each image or formula in the text (see `CopiedText`).
        if (e.ClickCount == 3 && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            SetCurrentValue(SelectionStartProperty, 0);
            SetCurrentValue(SelectionEndProperty, LayoutLength);
        }
    }

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
