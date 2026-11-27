using AvaloniaEdit.Document;

namespace Polhem.Markdown.Avalonia;

/// <summary>The range of a <see cref="MarkdownRangeMark"/>, kept by AvaloniaEdit's segment tree as the text changes.</summary>
internal sealed class RangeMarkSegment(MarkdownRangeMark mark) : TextSegment
{
    public MarkdownRangeMark Mark { get; } = mark;
}
