using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using Polhem.Markdown.Avalonia.Automation;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A paragraph, heading, table cell or the code of a code block: selectable text whose link runs can be clicked. A link either opens a URI or
/// moves to an anchor of the same text: a heading, a footnote, or a reference to one.
/// </summary>
/// <remarks>
/// NOTE: links are runs inside the text rather than controls placed in it, so a long link wraps like any other text and
/// a selection can run through it. A click is mapped to a character with the text layout and from there to a link. The
/// keyboard and assistive technologies reach a link through a <see cref="LinkFocusTarget"/> laid over it, which takes the
/// focus but not the pointer.
/// </remarks>
internal sealed class MarkdownTextBlock : SelectableTextBlock
{
    private readonly MarkdownViewer _viewer;
    private readonly List<LinkRange> _links = [];
    private readonly List<string> _anchorIds = [];
    private readonly Dictionary<int, string> _embeddedTexts = [];
    private readonly List<LinkFocusTarget> _targets = [];
    private string? _layoutText;

    public MarkdownTextBlock(MarkdownViewer viewer)
    {
        _viewer = viewer;

        // A text block without a background takes the pointer only where it draws glyphs. With one, a drag can start in
        // the space after a short line, as it can in a browser.
        Background = Brushes.Transparent;

        // Only links and buttons take part in Tab navigation, as in a browser; a text's links do through their targets. Any
        // text still takes the focus when it is clicked, for selecting and copying.
        IsTabStop = false;
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
            Padding = new Thickness(Padding.Left, Padding.Top, Padding.Right, extra);
        }
    }

    /// <summary>How far a line's own descenders reach below its baseline, as a fraction of the font size.</summary>
    private const double TextDescent = 0.22;

    /// <summary>The links of the text, by character range.</summary>
    public IReadOnlyList<LinkRange> Links => _links;

    protected override Type StyleKeyOverride => typeof(SelectableTextBlock);

    public void AddLink(int start, int length, Uri target) => AddLinkRange(new LinkRange(start, length, target, null, null));

    /// <summary>
    /// Records a link that moves to an anchor of the same text. <paramref name="name"/> is what an assistive technology
    /// reads for it instead of its text, as for the arrow of a footnote's back link.
    /// </summary>
    public void AddAnchorLink(int start, int length, string anchorId, string? name = null) =>
        AddLinkRange(new LinkRange(start, length, null, anchorId, name));

    private void AddLinkRange(LinkRange link)
    {
        _links.Add(link);
        _targets.Add(new LinkFocusTarget(this, _links.Count - 1));
    }

    /// <summary>Takes the text's links out of Tab navigation, for a text whose links do not open, such as a summary.</summary>
    public void ExcludeLinksFromTabOrder()
    {
        foreach (var target in _targets)
        {
            target.IsTabStop = false;
        }
    }

    /// <summary>The controls that stand for the text's links, in the order of the text.</summary>
    internal IReadOnlyList<LinkFocusTarget> LinkTargets => _targets;

    protected override Size ArrangeOverride(Size finalSize)
    {
        var focused = _targets.Find(target => target.IsFocused);
        var size = base.ArrangeOverride(finalSize);

        // NOTE: the targets join the visual children here rather than when their links are added, because a text block
        // with inline controls clears its visual children when it arranges them, and adds back only those controls. A
        // target taken off loses the focus, which it is given back once it is back.
        foreach (var target in _targets)
        {
            if (target.GetVisualParent() != this)
            {
                VisualChildren.Add(target);
            }
            target.Measure(default);
            target.Arrange(LinkBounds(target.Index));
        }
        if (focused is { IsFocused: false })
        {
            focused.Focus(NavigationMethod.Unspecified);
        }
        return size;
    }

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
    public string CopiedText => TextBetween(Math.Min(SelectionStart, SelectionEnd), Math.Max(SelectionStart, SelectionEnd));

    /// <summary>The whole text as an assistive technology reads it, which is also how it is copied.</summary>
    public string AccessibleText => TextBetween(0, LayoutLength);

    /// <summary>The text between two positions of the layout, with embedded controls as their text.</summary>
    private string TextBetween(int start, int end)
    {
        var text = LayoutText;
        start = Math.Clamp(start, 0, text.Length);
        end = Math.Clamp(end, 0, text.Length);
        if (start >= end)
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

    /// <summary>The target of the link at a character index of the text, or <see langword="null"/>.</summary>
    public Uri? LinkAt(int index) => LinkRangeAt(index)?.Target;

    /// <summary>The link at a character index of the text, or <see langword="null"/>.</summary>
    public LinkRange? LinkRangeAt(int index) =>
        _links.FirstOrDefault(link => index >= link.Start && index < link.Start + link.Length);

    /// <summary>The index of the link the keyboard is on, or -1.</summary>
    public int FocusedLink => _targets.FindIndex(target => target.IsFocused);

    /// <summary>What an assistive technology reads for a link: its text, or the name it was given.</summary>
    public string LinkName(int index) =>
        _links[index] is { Name: { } name } ? name : TextBetween(_links[index].Start, _links[index].Start + _links[index].Length);

    /// <summary>The rectangle around a link, in the text's own coordinates.</summary>
    public Rect LinkBounds(int index)
    {
        Rect? bounds = null;
        foreach (var rectangle in LinkRectangles(index))
        {
            bounds = bounds is { } union ? union.Union(rectangle) : rectangle;
        }
        return bounds ?? default;
    }

    /// <summary>The rectangles a link takes up, one for each line it is on, in the text's own coordinates.</summary>
    public IEnumerable<Rect> LinkRectangles(int index) =>
        TextLayout.HitTestTextRange(_links[index].Start, _links[index].Length)
            .Select(rectangle => rectangle.Translate(new Vector(Padding.Left, Padding.Top)));

    /// <summary>
    /// Follows a link: opens its URI, or moves to its anchor. From the keyboard, the focus moves to the anchor too, so the
    /// next Tab continues from there and a screen reader reads it, as in a browser.
    /// </summary>
    public void ActivateLink(int index, bool moveFocus = true)
    {
        var link = _links[index];
        if (link.AnchorId is { } anchorId)
        {
            _viewer.ScrollToAnchor(anchorId, moveFocus);
        }
        else if (link.Target is { } target)
        {
            _viewer.ActivateLink(this, target);
        }
    }

    /// <summary>The brush the focus rectangle around a link is drawn with.</summary>
    internal IBrush FocusBrush => _viewer.LinkForeground;

    protected override AutomationPeer OnCreateAutomationPeer() => new TextAutomationPeer(this);

    /// <summary>
    /// The text, which is also what a control that shows this text as its content is named by, such as the header of a
    /// collapsible section.
    /// </summary>
    public override string ToString() => AccessibleText;

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
        if (LinkUnder(e) is { } link)
        {
            ActivateLink(_links.IndexOf(link), moveFocus: false);
        }
    }

    private LinkRange? LinkUnder(PointerEventArgs e)
    {
        if (_links.Count == 0)
        {
            return null;
        }
        var point = e.GetPosition(this) - new Point(Padding.Left, Padding.Top);
        var hit = TextLayout.HitTestPoint(point);
        return hit.IsInside ? LinkRangeAt(hit.TextPosition) : null;
    }

    /// <summary>
    /// A link run: <see cref="Target"/> for a link that opens a URI, <see cref="AnchorId"/> for one within the text.
    /// <see cref="Name"/> replaces its text for an assistive technology.
    /// </summary>
    internal sealed record LinkRange(int Start, int Length, Uri? Target, string? AnchorId, string? Name);
}
