using Avalonia.Media;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A mark a host draws over a range of the source in <see cref="MarkdownEditor"/>, made by
/// <see cref="MarkdownEditor.AddMark"/>. Its range moves with the text as it is edited.
/// </summary>
public sealed class MarkdownRangeMark
{
    private readonly RangeMarkSegment _segment;
    private int _removedStart;
    private int _removedLength;

    internal MarkdownRangeMark(int start, int length, MarkdownRangeMarkStyle style, IBrush brush, object? owner)
    {
        Style = style;
        Brush = brush;
        Owner = owner;
        _segment = new RangeMarkSegment(this) { StartOffset = start, Length = length };
    }

    /// <summary>Gets where the range starts now, as an offset in <see cref="MarkdownEditor.Markdown"/>.</summary>
    /// <remarks>Once the mark is removed, it keeps where the range was when it was removed.</remarks>
    public int Start => IsRemoved ? _removedStart : _segment.StartOffset;

    /// <summary>Gets how many UTF-16 code units the range covers now.</summary>
    /// <remarks>Once the mark is removed, it keeps how long the range was when it was removed.</remarks>
    public int Length => IsRemoved ? _removedLength : _segment.Length;

    /// <summary>Gets how the mark is drawn.</summary>
    public MarkdownRangeMarkStyle Style { get; }

    /// <summary>Gets the brush the mark is drawn with.</summary>
    public IBrush Brush { get; }

    /// <summary>Gets the key the host grouped the mark under, or <see langword="null"/> for none.</summary>
    public object? Owner { get; }

    /// <summary>
    /// Gets whether the mark is no longer in the editor: removed by the host, or gone with its text when the text was
    /// deleted or <see cref="MarkdownEditor.Markdown"/> was set.
    /// </summary>
    public bool IsRemoved { get; private set; }

    /// <summary>The segment that moves the range with the edits, for the editor.</summary>
    internal RangeMarkSegment Segment => _segment;

    /// <summary>Keeps the range as it is now, before the segment leaves the editor's collection.</summary>
    internal void MarkRemoved()
    {
        _removedStart = _segment.StartOffset;
        _removedLength = _segment.Length;
        IsRemoved = true;
    }
}
