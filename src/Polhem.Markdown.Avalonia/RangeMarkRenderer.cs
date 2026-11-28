using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Polhem.Markdown.Avalonia;

/// <summary>Draws the host's range marks in the lines on the screen.</summary>
/// <remarks>
/// It draws on the background layer, which is the text view's own rendering: that puts every mark under the selection and
/// the text, and redraws it whenever the view is measured, as it is on every scroll and edit. The layers above are only
/// redrawn when AvaloniaEdit invalidates them, which it does not do on a vertical scroll.
/// </remarks>
/// <param name="marks">The marks to draw.</param>
/// <param name="backgroundOpacity">How opaque the background marks are drawn, over the opacity of their brushes.</param>
internal sealed class RangeMarkRenderer(TextSegmentCollection<RangeMarkSegment> marks, double backgroundOpacity = 1) : IBackgroundRenderer
{
    /// <summary>Where the strikethrough runs, as a fraction of the line's height from its top: about the middle of a lowercase letter.</summary>
    private const double StrikethroughPosition = 0.55;

    public KnownLayer Layer => KnownLayer.Background;

    public void Draw(TextView textView, DrawingContext drawingContext)
    {
        if (marks.Count == 0 || !textView.VisualLinesValid || textView.VisualLines.Count == 0)
        {
            return;
        }
        var visualLines = textView.VisualLines;
        var start = visualLines[0].FirstDocumentLine.Offset;
        var end = visualLines[^1].LastDocumentLine.EndOffset;
        var thickness = Math.Max(1, Math.Round(textView.DefaultLineHeight / 14));
        foreach (var segment in marks.FindOverlappingSegments(start, end - start))
        {
            var mark = segment.Mark;
            if (mark.Style == MarkdownRangeMarkStyle.Background)
            {
                var builder = new BackgroundGeometryBuilder { AlignToWholePixels = true, CornerRadius = 2 };
                builder.AddSegment(textView, segment);
                if (builder.CreateGeometry() is { } geometry)
                {
                    using (drawingContext.PushOpacity(backgroundOpacity))
                    {
                        drawingContext.DrawGeometry(mark.Brush, null, geometry);
                    }
                }
                continue;
            }
            var pen = new Pen(mark.Brush, thickness);
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, segment))
            {
                var y = mark.Style == MarkdownRangeMarkStyle.Underline
                    ? rect.Bottom - thickness
                    : rect.Top + rect.Height * StrikethroughPosition;
                // NOTE: a line of odd thickness is crisp when its centre sits half a pixel off the pixel grid.
                y = Math.Round(y) + thickness % 2 / 2;
                drawingContext.DrawLine(pen, new Point(rect.Left, y), new Point(rect.Right, y));
            }
        }
    }
}
