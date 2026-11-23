namespace Polhem.Markdown.Diagrams;

/// <summary>
/// Collects the shapes of a diagram in drawing order and, at the end, moves them so the drawing starts at a small margin
/// whatever coordinates the layout used.
/// </summary>
internal sealed class DrawingBuilder
{
    /// <summary>The stroke thickness of a connector.</summary>
    public const double LineThickness = 1.5;

    /// <summary>The stroke thickness of a thick connector.</summary>
    public const double ThickLineThickness = 3;

    private const double Margin = 8;
    private const double ArrowLength = 9;
    private const double ArrowHalfWidth = 4.5;

    private readonly List<DiagramShape> _shapes = [];

    public void Rectangle(double x, double y, double width, double height, double cornerRadius) =>
        _shapes.Add(new DiagramRectangle(x, y, width, height, cornerRadius));

    public void Polygon(IReadOnlyList<DiagramPoint> points) => _shapes.Add(new DiagramPolygon(points, IsArrowHead: false));

    public void Line(IReadOnlyList<DiagramPoint> points, DiagramStroke stroke, double thickness, double dash) =>
        _shapes.Add(new DiagramLine(points, stroke, thickness, dash));

    /// <summary>
    /// Adds a connector in the text color, ending in an arrowhead whose tip is the last point when
    /// <paramref name="arrow"/> is set.
    /// </summary>
    public void Connector(IReadOnlyList<DiagramPoint> points, double thickness, double dash, bool arrow)
    {
        if (!arrow)
        {
            Line(points, DiagramStroke.Text, thickness, dash);
            return;
        }

        var tip = points[^1];
        var before = points[^2];
        var dx = tip.X - before.X;
        var dy = tip.Y - before.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        if (length < 0.001)
        {
            Line(points, DiagramStroke.Text, thickness, dash);
            return;
        }
        var ux = dx / length;
        var uy = dy / length;

        // The line stops at the base of the arrowhead, so a thick or dashed line does not show through its tip.
        var size = thickness > LineThickness ? 1.3 : 1.0;
        var arrowLength = Math.Min(ArrowLength * size, length);
        var baseX = tip.X - ux * arrowLength;
        var baseY = tip.Y - uy * arrowLength;
        var shortened = points.Take(points.Count - 1).Append(new DiagramPoint(baseX, baseY)).ToList();
        Line(shortened, DiagramStroke.Text, thickness, dash);

        var halfWidth = ArrowHalfWidth * size;
        _shapes.Add(new DiagramPolygon(
            [
                tip,
                new DiagramPoint(baseX - uy * halfWidth, baseY + ux * halfWidth),
                new DiagramPoint(baseX + uy * halfWidth, baseY - ux * halfWidth),
            ],
            IsArrowHead: true));
    }

    /// <summary>Adds lines of text centered on (<paramref name="x"/>, <paramref name="y"/>) as a block.</summary>
    public void Text(double x, double y, IReadOnlyList<string> lines)
    {
        var top = y - TextMetrics.Height(lines) / 2;
        for (var index = 0; index < lines.Count; index++)
        {
            if (lines[index].Length > 0)
            {
                _shapes.Add(new DiagramText(x, top + TextMetrics.LineHeight * (index + 0.5), lines[index]));
            }
        }
    }

    /// <summary>Adds lines of text whose block starts at <paramref name="left"/> and is centered on <paramref name="y"/>.</summary>
    public void TextFrom(double left, double y, IReadOnlyList<string> lines) =>
        Text(left + TextMetrics.Width(lines) / 2, y, lines);

    public DiagramDrawing Build()
    {
        if (_shapes.Count == 0)
        {
            return new DiagramDrawing(2 * Margin, 2 * Margin, []);
        }

        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        foreach (var (left, top, right, bottom) in _shapes.Select(shape => shape.Bounds()))
        {
            minX = Math.Min(minX, left);
            minY = Math.Min(minY, top);
            maxX = Math.Max(maxX, right);
            maxY = Math.Max(maxY, bottom);
        }

        var dx = Margin - minX;
        var dy = Margin - minY;
        var shapes = _shapes.Select(shape => shape.MovedBy(dx, dy)).ToList();
        return new DiagramDrawing(Round(maxX - minX + 2 * Margin), Round(maxY - minY + 2 * Margin), shapes);
    }

    // Two decimals are below what a screen shows and keep the SVG short.
    private static double Round(double value) => Math.Round(value, 2);
}
