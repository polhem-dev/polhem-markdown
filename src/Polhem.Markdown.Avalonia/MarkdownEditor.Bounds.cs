using Avalonia;
using Avalonia.Controls;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Polhem.Markdown.Avalonia;

// Where the text is on the screen, for a host that places a popup next to it.
public partial class MarkdownEditor
{
    /// <summary>Where the text view was in the editor when <see cref="ViewChanged"/> was last raised.</summary>
    private Point? _reportedTextViewOrigin;

    /// <summary>
    /// Occurs when the text may have moved on the screen: the view scrolled, the text was laid out again after an edit,
    /// or the editor changed size. Bounds from <see cref="GetTextBounds"/> taken before it may be out of date.
    /// </summary>
    /// <remarks>It can occur several times for one change, and when nothing a host placed has moved.</remarks>
    public event EventHandler? ViewChanged;

    /// <summary>
    /// Gets where <paramref name="length"/> characters from <paramref name="start"/> are on the screen, in the editor's
    /// coordinates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A range over several lines gives the box around all of them. A length of 0 gives the place of a caret at
    /// <paramref name="start"/>: as tall as its line, and 0 wide.
    /// </para>
    /// <para>
    /// Only the lines in view are laid out, so a range scrolled out of view, or an editor not shown, gives
    /// <see langword="null"/>; of a range partly in view, the box covers the lines in view, and may reach past the
    /// editor's edge. <see cref="ViewChanged"/> tells when to ask again.
    /// </para>
    /// </remarks>
    /// <param name="start">Where the range starts, as an offset in <see cref="Markdown"/>.</param>
    /// <param name="length">How many UTF-16 code units the range covers: 0 for a single place.</param>
    /// <returns>The box around the range, or <see langword="null"/> when none of it is in view.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is not within the text.</exception>
    public Rect? GetTextBounds(int start, int length)
    {
        var document = _editor.Document;
        CheckRange(start, length, document.TextLength);
        var textView = _editor.TextArea.TextView;
        if (TopLevel.GetTopLevel(textView) is null || textView.Bounds.Width <= 0 || textView.Bounds.Height <= 0)
        {
            return null;
        }
        textView.EnsureVisualLines();
        var bounds = length == 0 ? GetCaretBounds(textView, document, start) : GetRangeBounds(textView, start, length);
        if (bounds is not { } box || textView.TranslatePoint(default, this) is not { } origin)
        {
            return null;
        }
        return box.Translate(new Vector(origin.X, origin.Y));
    }

    private static Rect? GetCaretBounds(TextView textView, TextDocument document, int offset)
    {
        var location = document.GetLocation(offset);
        if (!textView.VisualLines.Any(line =>
            line.FirstDocumentLine.LineNumber <= location.Line && location.Line <= line.LastDocumentLine.LineNumber))
        {
            return null;
        }
        var position = new TextViewPosition(location);
        var scroll = textView.ScrollOffset;
        var top = textView.GetVisualPosition(position, VisualYPosition.LineTop) - scroll;
        var bottom = textView.GetVisualPosition(position, VisualYPosition.LineBottom) - scroll;
        return new Rect(top, new Point(top.X, bottom.Y));
    }

    private static Rect? GetRangeBounds(TextView textView, int start, int length)
    {
        Rect? bounds = null;
        foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, new TextSegment { StartOffset = start, Length = length }))
        {
            bounds = bounds is { } box ? box.Union(rect) : rect;
        }
        return bounds;
    }

    private void OnViewChanged()
    {
        UpdateFirstVisibleSourceOffset();
        ViewChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Raises <see cref="ViewChanged"/> when the text view moves within the editor, as it does when the toolbar shows or
    /// hides, which does not always lay the text out again.
    /// </summary>
    private void ReportTextViewMoved()
    {
        var origin = _editor.TextArea.TextView.TranslatePoint(default, this);
        if (origin != _reportedTextViewOrigin)
        {
            _reportedTextViewOrigin = origin;
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
