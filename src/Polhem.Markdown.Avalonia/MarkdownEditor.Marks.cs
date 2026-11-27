using Avalonia.Media;
using AvaloniaEdit.Document;

namespace Polhem.Markdown.Avalonia;

// Marks the host draws over ranges of the source, such as a change shown before it is made.
public partial class MarkdownEditor
{
    /// <summary>The marks, in a tree AvaloniaEdit moves with the edits, updated by <see cref="OnDocumentChanged"/>.</summary>
    private readonly TextSegmentCollection<RangeMarkSegment> _marks = [];

    /// <summary>Gets the marks in the editor, in the order their ranges start.</summary>
    public IReadOnlyList<MarkdownRangeMark> Marks => [.. _marks.Select(segment => segment.Mark)];

    /// <summary>
    /// Marks <paramref name="length"/> characters from <paramref name="start"/>, drawn in <paramref name="style"/> with
    /// <paramref name="brush"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The mark moves with the text as it is edited, by the user or from code. Text typed inside the range is part of it;
    /// text inserted where the range starts or ends is not. When all of the range's text is deleted, the mark goes with
    /// it, and undoing the deletion does not bring it back. Setting <see cref="Markdown"/> removes every mark.
    /// </para>
    /// <para>
    /// Marks are drawn under the selection and the text, and do not change the syntax colours. Marks may overlap, and are
    /// drawn in the order their ranges start.
    /// </para>
    /// </remarks>
    /// <param name="start">Where the range starts, as an offset in <see cref="Markdown"/>.</param>
    /// <param name="length">How many UTF-16 code units the range covers: at least 1.</param>
    /// <param name="style">How the mark is drawn.</param>
    /// <param name="brush">The brush the mark is drawn with.</param>
    /// <param name="owner">
    /// A key that groups the mark with others, so <see cref="ClearMarks(object)"/> removes them together, or
    /// <see langword="null"/> for none.
    /// </param>
    /// <returns>The mark, which tells where its range is as the text changes.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="brush"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The range is not within the text, its length is 0, or <paramref name="style"/> is not defined.
    /// </exception>
    public MarkdownRangeMark AddMark(int start, int length, MarkdownRangeMarkStyle style, IBrush brush, object? owner = null)
    {
        ArgumentNullException.ThrowIfNull(brush);
        CheckRange(start, length, _editor.Document.TextLength);
        ArgumentOutOfRangeException.ThrowIfZero(length);
        if (!Enum.IsDefined(style))
        {
            throw new ArgumentOutOfRangeException(nameof(style), style, "The style is not defined.");
        }
        var mark = new MarkdownRangeMark(start, length, style, brush, owner);
        _marks.Add(mark.Segment);
        RedrawMarks();
        return mark;
    }

    /// <summary>Removes <paramref name="mark"/> from the editor.</summary>
    /// <param name="mark">The mark to remove.</param>
    /// <returns>Whether the mark was in the editor.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="mark"/> is <see langword="null"/>.</exception>
    public bool RemoveMark(MarkdownRangeMark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);
        if (mark.IsRemoved || !_marks.Contains(mark.Segment))
        {
            return false;
        }
        mark.MarkRemoved();
        _marks.Remove(mark.Segment);
        RedrawMarks();
        return true;
    }

    /// <summary>Removes every mark from the editor.</summary>
    public void ClearMarks() => RemoveMarks(_ => true);

    /// <summary>Removes the marks grouped under <paramref name="owner"/>, and leaves the others.</summary>
    /// <param name="owner">The key the marks were added with, compared with <see cref="object.Equals(object?)"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is <see langword="null"/>.</exception>
    public void ClearMarks(object owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        RemoveMarks(segment => Equals(segment.Mark.Owner, owner));
    }

    /// <summary>Removes the marks whose text was all deleted, once a change or a group of changes ends.</summary>
    private void RemoveEmptyMarks() => RemoveMarks(segment => segment.Length == 0);

    private void RemoveMarks(Func<RangeMarkSegment, bool> match)
    {
        var removed = _marks.Where(match).ToList();
        if (removed.Count == 0)
        {
            return;
        }
        foreach (var segment in removed)
        {
            segment.Mark.MarkRemoved();
            _marks.Remove(segment);
        }
        RedrawMarks();
    }

    private void RedrawMarks() => _editor.TextArea.TextView.InvalidateVisual();
}
