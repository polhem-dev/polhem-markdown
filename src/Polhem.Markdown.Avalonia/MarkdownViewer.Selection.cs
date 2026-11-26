using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Polhem.Markdown.Avalonia;

// A selection that spans blocks (polhem-dev/polhem-markdown#1). Each block is its own selectable text, and Avalonia keeps
// a selection inside the text it started in. A drag that stays in one text is left to that text, which also selects
// whole words after a double click. Once the pointer reaches another text, the viewer selects the end of the first, the
// texts in between in full, and the start of the last, as a browser does.
public partial class MarkdownViewer
{
    /// <summary>
    /// What a control groups: the items of a list or the rows of a table. The text copied from a selection puts a line
    /// break between two texts of one group, and an empty line between other blocks.
    /// </summary>
    internal static readonly AttachedProperty<TextGroup> TextGroupProperty =
        AvaloniaProperty.RegisterAttached<MarkdownViewer, Control, TextGroup>("TextGroup");

    /// <summary>The row of a table cell's text, which the copied text separates from the next cell of the row by a tab.</summary>
    internal static readonly AttachedProperty<int> TableRowProperty =
        AvaloniaProperty.RegisterAttached<MarkdownViewer, Control, int>("TableRow", -1);

    /// <summary>How far the pointer may be outside the scroll viewer, in pixels, before scrolling stops getting faster.</summary>
    private const double MaxScrollStep = 40;

    /// <summary>The texts that can be selected, in document order, while a drag goes on; <see langword="null"/> otherwise.</summary>
    private List<MarkdownTextBlock>? _dragTexts;

    /// <summary>Where a drag started: the text, and the part of it the press selected (one position, or a word).</summary>
    private SelectionAnchor? _anchor;

    /// <summary>The end of a selection the viewer spreads over several texts, or <see langword="null"/>.</summary>
    private SelectionEnd? _end;

    /// <summary>The scroll viewer that scrolls while a drag is above or below it, and where the pointer last was in it.</summary>
    private ScrollViewer? _dragScroller;
    private Point _dragPointer;
    private DispatcherTimer? _scrollTimer;

    /// <summary>
    /// The selected text of the whole document, in document order: the selected part of each text, with a line break
    /// between items of a list and rows of a table, a tab between cells of a row, and an empty line between other blocks.
    /// </summary>
    internal string SelectedText
    {
        get
        {
            var builder = new StringBuilder();
            MarkdownTextBlock? previous = null;
            foreach (var text in Texts())
            {
                var copied = text.CopiedText;
                if (copied.Length == 0)
                {
                    continue;
                }
                if (previous is not null)
                {
                    builder.Append(Separator(previous, text));
                }
                builder.Append(copied);
                previous = text;
            }
            return builder.ToString();
        }
    }

    /// <summary>Whether more than one text has a selection, which only the viewer can copy as a whole.</summary>
    internal bool SpansTexts => Texts().Count(text => text.SelectionStart != text.SelectionEnd) > 1;

    /// <summary>Selects the text of the whole document.</summary>
    internal void SelectAll()
    {
        var texts = Texts();
        if (texts.Count == 0)
        {
            return;
        }
        _anchor = new SelectionAnchor(texts[0], 0, 0);
        _end = new SelectionEnd(texts[^1], TextLength(texts[^1]));
        Apply(texts);
    }

    /// <summary>Clears the selection in every text.</summary>
    internal void ClearSelection()
    {
        _anchor = null;
        _end = null;
        foreach (var text in Texts())
        {
            if (text.SelectionStart != text.SelectionEnd)
            {
                text.ClearSelection();
            }
        }
    }

    /// <summary>
    /// Whether a text that loses the focus to <paramref name="newFocus"/> keeps its part of a selection that spans texts.
    /// Avalonia clears a text's selection when it loses the focus, which would cut a hole in the selection when a right
    /// click focuses another text of it. When the focus leaves the viewer, the whole selection is cleared instead, as a
    /// single text clears its own.
    /// </summary>
    internal bool KeepsSelectionOnLostFocus(MarkdownTextBlock text, IInputElement? newFocus)
    {
        if (_end is null)
        {
            return false;
        }
        if (newFocus is Visual visual && (visual == this || this.IsVisualAncestorOf(visual)))
        {
            return true;
        }
        if (text.ContextFlyout?.IsOpen == true || text.ContextMenu?.IsOpen == true)
        {
            return true;
        }
        Dispatcher.UIThread.Post(ClearSelection);
        return false;
    }

    /// <summary>Applies a selection that spans texts again after a rebuild, or clears it when a text it ends in is gone.</summary>
    private void KeepSelection()
    {
        _dragTexts = null;
        if (_anchor is not { } anchor || _end is not { } end)
        {
            return;
        }
        var texts = Texts();
        if (!texts.Contains(anchor.Text) || !texts.Contains(end.Text))
        {
            ClearSelection();
            return;
        }
        Apply(texts);
    }

    private void OnPointerPressedForSelection(object? sender, PointerPressedEventArgs e)
    {
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            // A right click inside the selection keeps it, for the context menu's copy; elsewhere it clears it.
            if (_end is not null && !(TextAt(e.Source) is { } text && text.SelectionStart != text.SelectionEnd))
            {
                ClearSelection();
            }
            return;
        }
        if (!point.Properties.IsLeftButtonPressed || e.Handled)
        {
            return;
        }

        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift) && TextAt(e.Source) is { } pressed && ExtendTo(pressed, e))
        {
            e.Handled = true;
            return;
        }
        ClearSelection();
    }

    /// <summary>
    /// Extends the selection to where a shift-click on <paramref name="pressed"/> falls, when the selection is in another
    /// text or already spans texts. A shift-click inside the one text that holds the selection is left to that text.
    /// </summary>
    private bool ExtendTo(MarkdownTextBlock pressed, PointerPressedEventArgs e)
    {
        var texts = Texts();
        if (_anchor is null)
        {
            var selected = texts.FirstOrDefault(text => text.SelectionStart != text.SelectionEnd);
            if (selected is null || selected == pressed)
            {
                return false;
            }
            var low = Math.Min(selected.SelectionStart, selected.SelectionEnd);
            var high = Math.Max(selected.SelectionStart, selected.SelectionEnd);
            _anchor = new SelectionAnchor(selected, low, high);
        }
        StartDrag(texts);
        e.Pointer.Capture(this);
        _end = new SelectionEnd(pressed, IndexAt(pressed, e.GetPosition(pressed)));
        Apply(texts);
        return true;
    }

    private void OnPointerPressedAfterText(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || _dragTexts is not null
            || TextAt(e.Source) is not { } text || e.Pointer.Captured != text)
        {
            return;
        }

        // The text has handled the press by now: the selection it made is where the drag starts.
        var low = Math.Min(text.SelectionStart, text.SelectionEnd);
        var high = Math.Max(text.SelectionStart, text.SelectionEnd);
        _anchor = new SelectionAnchor(text, low, high);
        _end = null;
        StartDrag(Texts());
    }

    private void StartDrag(List<MarkdownTextBlock> texts)
    {
        _dragTexts = texts;
        _dragScroller = this.FindAncestorOfType<ScrollViewer>();
    }

    private void OnPointerMovedForSelection(object? sender, PointerEventArgs e)
    {
        if (_dragTexts is null || _anchor is null)
        {
            return;
        }
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            EndDrag();
            return;
        }
        if (_dragScroller is { } scroller)
        {
            _dragPointer = e.GetPosition(scroller);
            UpdateScrolling(scroller);
        }
        Drag(e.GetPosition(this), force: false);
    }

    /// <summary>
    /// Follows the pointer to <paramref name="point"/>, in the viewer's coordinates. While the pointer stays in the text
    /// the drag started in, and the selection has not spread to other texts, that text keeps selecting on its own.
    /// </summary>
    private void Drag(Point point, bool force)
    {
        if (_dragTexts is not { } texts || _anchor is not { } anchor)
        {
            return;
        }
        var (text, index) = Hit(texts, point);
        if (text is null)
        {
            return;
        }
        if (text == anchor.Text && _end is null && !force)
        {
            return;
        }
        _end = new SelectionEnd(text, index);
        Apply(texts);
    }

    private void OnPointerReleasedForSelection(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragTexts is not null)
        {
            if (e.Pointer.Captured == this)
            {
                e.Pointer.Capture(null);
            }
            EndDrag();
        }
    }

    private void OnPointerCaptureLostForSelection(object? sender, PointerCaptureLostEventArgs e)
    {
        // A text releases the pointer when the button goes up; the viewer takes the pointer only for a shift-click.
        if (_dragTexts is not null && e.Source is not MarkdownTextBlock)
        {
            EndDrag();
        }
    }

    private void EndDrag()
    {
        _dragTexts = null;
        _dragScroller = null;
        _scrollTimer?.Stop();
    }

    /// <summary>Scrolls while the pointer is above or below the scroll viewer, faster the further out it is.</summary>
    private void UpdateScrolling(ScrollViewer scroller)
    {
        var outside = Overshoot(scroller);
        if (outside == 0)
        {
            _scrollTimer?.Stop();
            return;
        }
        if (_scrollTimer is null)
        {
            _scrollTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) };
            _scrollTimer.Tick += (_, _) => ScrollStep();
        }
        if (!_scrollTimer.IsEnabled)
        {
            _scrollTimer.Start();
        }
    }

    private double Overshoot(ScrollViewer scroller)
    {
        var height = scroller.Viewport.Height > 0 ? scroller.Viewport.Height : scroller.Bounds.Height;
        return _dragPointer.Y < 0 ? _dragPointer.Y : _dragPointer.Y > height ? _dragPointer.Y - height : 0;
    }

    /// <summary>One step of scrolling during a drag, after which the selection follows the pointer to the new content.</summary>
    internal void ScrollStep()
    {
        if (_dragScroller is not { } scroller)
        {
            _scrollTimer?.Stop();
            return;
        }
        var outside = Overshoot(scroller);
        if (outside == 0)
        {
            _scrollTimer?.Stop();
            return;
        }
        var step = Math.Clamp(outside, -MaxScrollStep, MaxScrollStep);
        var bottom = Math.Max(0, scroller.Extent.Height - scroller.Viewport.Height);
        scroller.Offset = new Vector(scroller.Offset.X, Math.Clamp(scroller.Offset.Y + step, 0, bottom));
        scroller.UpdateLayout();
        if (scroller.TranslatePoint(_dragPointer, this) is { } point)
        {
            Drag(point, force: true);
        }
    }

    private void OnKeyDownForSelection(object? sender, KeyEventArgs e)
    {
        if (Application.Current?.PlatformSettings?.HotkeyConfiguration is not { } keys)
        {
            return;
        }
        if (keys.SelectAll.Any(gesture => gesture.Matches(e)))
        {
            SelectAll();
            e.Handled = true;
        }
        else if (keys.Copy.Any(gesture => gesture.Matches(e)) && SelectedText.Length > 0)
        {
            _ = CopySelectionAsync();
            e.Handled = true;
        }
    }

    /// <summary>
    /// Copies the whole selection instead of a text's own part when a text's context menu copies it, and copies images
    /// and formulas as text rather than as object replacement characters.
    /// </summary>
    private void OnCopyingToClipboard(object? sender, RoutedEventArgs e)
    {
        if (SelectedText.Length > 0)
        {
            _ = CopySelectionAsync();
            e.Handled = true;
        }
    }

    private async Task CopySelectionAsync()
    {
        var text = SelectedText;
        if (text.Length > 0 && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }

    /// <summary>
    /// Sets each text's part of the selection between <see cref="_anchor"/> and <see cref="_end"/>: part of the first and
    /// the last, all of those between, and none of the rest.
    /// </summary>
    private void Apply(List<MarkdownTextBlock> texts)
    {
        if (_anchor is not { } anchor || _end is not { } end)
        {
            return;
        }
        var anchorIndex = texts.IndexOf(anchor.Text);
        var endIndex = texts.IndexOf(end.Text);
        if (anchorIndex < 0 || endIndex < 0)
        {
            return;
        }

        // A drag forward keeps the start of what the press selected, a drag back its end, so a word selected by a double
        // click stays selected whichever way the drag goes.
        var forward = endIndex > anchorIndex || (endIndex == anchorIndex && end.Index >= anchor.Low);
        var (first, firstAt, last, lastAt) = forward
            ? (anchorIndex, anchor.Low, endIndex, end.Index)
            : (endIndex, end.Index, anchorIndex, anchor.High);

        for (var index = 0; index < texts.Count; index++)
        {
            var text = texts[index];
            if (index < first || index > last)
            {
                if (text.SelectionStart != text.SelectionEnd)
                {
                    text.ClearSelection();
                }
                continue;
            }
            var start = index == first ? firstAt : 0;
            var stop = index == last ? lastAt : TextLength(text);
            text.SetCurrentValue(SelectableTextBlock.SelectionStartProperty, start);
            text.SetCurrentValue(SelectableTextBlock.SelectionEndProperty, stop);
        }
    }

    /// <summary>
    /// The text at <paramref name="point"/>, in the viewer's coordinates, and the position in it: the text under the
    /// point; otherwise the nearest text on the same line, such as a table cell beside it; otherwise the end of the last
    /// text above the point, or the start of the first text when the point is above them all.
    /// </summary>
    private (MarkdownTextBlock? Text, int Index) Hit(List<MarkdownTextBlock> texts, Point point)
    {
        MarkdownTextBlock? beside = null;
        var besideDistance = double.MaxValue;
        MarkdownTextBlock? above = null;
        foreach (var text in texts)
        {
            if (text.TransformToVisual(this) is not { } transform)
            {
                continue;
            }
            var bounds = new Rect(text.Bounds.Size).TransformToAABB(transform);
            if (bounds.Contains(point))
            {
                return (text, IndexAt(text, point - bounds.TopLeft));
            }
            if (point.Y >= bounds.Top && point.Y < bounds.Bottom)
            {
                var distance = point.X < bounds.Left ? bounds.Left - point.X : point.X - bounds.Right;
                if (distance < besideDistance)
                {
                    beside = text;
                    besideDistance = distance;
                }
            }
            else if (bounds.Bottom <= point.Y)
            {
                above = text;
            }
        }
        if (beside is not null && beside.TransformToVisual(this) is { } besideTransform)
        {
            var origin = new Rect(beside.Bounds.Size).TransformToAABB(besideTransform).TopLeft;
            return (beside, IndexAt(beside, point - origin));
        }
        if (above is not null)
        {
            return (above, TextLength(above));
        }
        return texts.Count == 0 ? (null, 0) : (texts[0], 0);
    }

    /// <summary>The position in <paramref name="text"/> at <paramref name="point"/>, in the text's coordinates, kept inside its lines.</summary>
    private static int IndexAt(MarkdownTextBlock text, Point point)
    {
        var layout = text.TextLayout;
        var inside = point - new Point(text.Padding.Left, text.Padding.Top);
        var clamped = new Point(
            Math.Clamp(inside.X, 0, Math.Max(layout.WidthIncludingTrailingWhitespace, 0)),
            Math.Clamp(inside.Y, 0, Math.Max(layout.Height, 0)));
        return layout.HitTestPoint(clamped).TextPosition;
    }

    private static int TextLength(MarkdownTextBlock text) => text.LayoutLength;

    /// <summary>The texts that can be selected, in document order: those shown, which leaves out a closed section's.</summary>
    private List<MarkdownTextBlock> Texts() =>
        _panel.GetVisualDescendants().OfType<MarkdownTextBlock>().Where(text => text.IsEffectivelyVisible).ToList();

    private static MarkdownTextBlock? TextAt(object? source) =>
        source as MarkdownTextBlock ?? (source as Visual)?.FindAncestorOfType<MarkdownTextBlock>();

    /// <summary>What goes between the selected parts of two texts in the copied text.</summary>
    private string Separator(MarkdownTextBlock previous, MarkdownTextBlock next)
    {
        var group = SharedGroup(previous, next);
        if (group == TextGroup.Table && previous.GetValue(TableRowProperty) == next.GetValue(TableRowProperty))
        {
            return "\t";
        }
        return group == TextGroup.None ? "\n\n" : "\n";
    }

    /// <summary>The innermost list or table that holds both texts, if any.</summary>
    private TextGroup SharedGroup(MarkdownTextBlock previous, MarkdownTextBlock next)
    {
        var ancestors = previous.GetVisualAncestors().TakeWhile(visual => visual != this).ToHashSet();
        foreach (var visual in next.GetVisualAncestors().TakeWhile(visual => visual != this))
        {
            if (ancestors.Contains(visual) && visual is Control control && control.GetValue(TextGroupProperty) != TextGroup.None)
            {
                return control.GetValue(TextGroupProperty);
            }
        }
        return TextGroup.None;
    }

    /// <summary>What a control groups, for the separators of copied text.</summary>
    internal enum TextGroup
    {
        None,
        List,
        Table,
    }

    /// <summary>The text a drag started in, and the part of it the press selected.</summary>
    private readonly record struct SelectionAnchor(MarkdownTextBlock Text, int Low, int High);

    /// <summary>The text and position a selection that spans texts ends at.</summary>
    private readonly record struct SelectionEnd(MarkdownTextBlock Text, int Index);
}
