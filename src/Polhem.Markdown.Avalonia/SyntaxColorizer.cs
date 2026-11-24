using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using Polhem.Markdown.Editing;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Colours the lines of a <see cref="MarkdownEditor"/> from the spans <see cref="MarkdownSyntaxScanner"/> found, with the
/// editor's brushes.
/// </summary>
/// <remarks>
/// The spans come from a scan of an earlier text. Between an edit and the next scan, <see cref="Shift"/> moves them with
/// the edit, so the colours stay on their characters while someone types.
/// </remarks>
internal sealed class SyntaxColorizer(MarkdownEditor editor) : DocumentColorizingTransformer
{
    private List<SyntaxSpan> _spans = [];

    /// <summary>The spans in use, in order and not overlapping.</summary>
    public IReadOnlyList<SyntaxSpan> Spans => _spans;

    /// <summary>Replaces the spans with those of a new scan.</summary>
    public void SetSpans(IReadOnlyList<SyntaxSpan> spans) => _spans = [.. spans];

    /// <summary>
    /// Moves the spans with an edit: those after it move by the change in length, a span the edit falls inside grows or
    /// shrinks with it, and one the edit cuts through is dropped until the next scan.
    /// </summary>
    public void Shift(int offset, int removed, int inserted)
    {
        var delta = inserted - removed;
        var editEnd = offset + removed;
        var shifted = new List<SyntaxSpan>(_spans.Count);
        foreach (var span in _spans)
        {
            var range = span.Range;
            if (range.End <= offset)
            {
                shifted.Add(span);
            }
            else if (range.Start >= editEnd)
            {
                shifted.Add(span with { Range = new SourceRange(range.Start + delta, range.Length) });
            }
            else if (range.Start < offset && range.End >= editEnd)
            {
                shifted.Add(span with { Range = new SourceRange(range.Start, range.Length + delta) });
            }
        }
        _spans = shifted;
    }

    /// <inheritdoc/>
    protected override void ColorizeLine(DocumentLine line)
    {
        var lineStart = line.Offset;
        var lineEnd = line.EndOffset;
        for (var index = FirstEndingAfter(lineStart); index < _spans.Count && _spans[index].Range.Start < lineEnd; index++)
        {
            var span = _spans[index];
            var start = Math.Max(span.Range.Start, lineStart);
            var end = Math.Min(span.Range.End, lineEnd);
            if (start >= end)
            {
                continue;
            }
            var brush = editor.BrushFor(span.Kind);
            var bold = span.Kind == MarkdownSyntaxKind.Heading || span.Style.HasFlag(MarkdownTextStyles.Bold);
            var italic = span.Style.HasFlag(MarkdownTextStyles.Italic);
            var struck = span.Style.HasFlag(MarkdownTextStyles.Strikethrough);
            ChangeLinePart(start, end, element =>
            {
                if (brush is not null)
                {
                    element.TextRunProperties.SetForegroundBrush(brush);
                }
                if (bold || italic)
                {
                    var typeface = element.TextRunProperties.Typeface;
                    element.TextRunProperties.SetTypeface(new Typeface(
                        typeface.FontFamily,
                        italic ? FontStyle.Italic : typeface.Style,
                        bold ? FontWeight.Bold : typeface.Weight,
                        typeface.Stretch));
                }
                if (struck)
                {
                    element.TextRunProperties.SetTextDecorations(TextDecorations.Strikethrough);
                }
            });
        }
    }

    /// <summary>The index of the first span that ends after <paramref name="offset"/>.</summary>
    private int FirstEndingAfter(int offset)
    {
        int low = 0, high = _spans.Count;
        while (low < high)
        {
            var middle = (low + high) / 2;
            if (_spans[middle].Range.End <= offset)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }
        return low;
    }
}
