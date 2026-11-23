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
        foreach (var (left, top, right, bottom) in _shapes.Select(Bounds))
        {
            minX = Math.Min(minX, left);
            minY = Math.Min(minY, top);
            maxX = Math.Max(maxX, right);
            maxY = Math.Max(maxY, bottom);
        }

        var dx = Margin - minX;
        var dy = Margin - minY;
        var shapes = _shapes.Select(shape => Offset(shape, dx, dy)).ToList();
        return new DiagramDrawing(Round(maxX - minX + 2 * Margin), Round(maxY - minY + 2 * Margin), shapes);
    }

    private static (double Left, double Top, double Right, double Bottom) Bounds(DiagramShape shape)
    {
        switch (shape)
        {
            case DiagramRectangle rectangle:
                return (rectangle.X, rectangle.Y, rectangle.X + rectangle.Width, rectangle.Y + rectangle.Height);
            case DiagramPolygon polygon:
                return PointBounds(polygon.Points, 0);
            case DiagramLine line:
                return PointBounds(line.Points, line.Thickness / 2);
            case DiagramText text:
                var halfWidth = TextMetrics.Width(text.Text) / 2;
                return (text.X - halfWidth, text.Y - TextMetrics.LineHeight / 2, text.X + halfWidth, text.Y + TextMetrics.LineHeight / 2);
            default:
                throw new ArgumentOutOfRangeException(nameof(shape), shape.GetType().Name);
        }
    }

    private static (double, double, double, double) PointBounds(IReadOnlyList<DiagramPoint> points, double pad) =>
        (points.Min(p => p.X) - pad, points.Min(p => p.Y) - pad, points.Max(p => p.X) + pad, points.Max(p => p.Y) + pad);

    private static DiagramShape Offset(DiagramShape shape, double dx, double dy) => shape switch
    {
        DiagramRectangle rectangle => rectangle with
        {
            X = Round(rectangle.X + dx),
            Y = Round(rectangle.Y + dy),
            Width = Round(rectangle.Width),
            Height = Round(rectangle.Height),
            CornerRadius = Round(rectangle.CornerRadius),
        },
        DiagramPolygon polygon => polygon with { Points = Offset(polygon.Points, dx, dy) },
        DiagramLine line => line with { Points = Offset(line.Points, dx, dy) },
        DiagramText text => text with { X = Round(text.X + dx), Y = Round(text.Y + dy) },
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape.GetType().Name),
    };

    private static List<DiagramPoint> Offset(IReadOnlyList<DiagramPoint> points, double dx, double dy) =>
        points.Select(p => new DiagramPoint(Round(p.X + dx), Round(p.Y + dy))).ToList();

    // Two decimals are below what a screen shows and keep the SVG short.
    private static double Round(double value) => Math.Round(value, 2);
}
