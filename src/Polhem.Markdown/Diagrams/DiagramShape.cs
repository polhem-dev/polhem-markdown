namespace Polhem.Markdown.Diagrams;

// NOTE: the primitives of a laid-out diagram share this file because they are one closed set that every viewer
// translates case by case; the viewers' shape coverage tests compare what they drew with the subclasses found here.

/// <summary>A point of a diagram, in the drawing's units (pixels at <see cref="DiagramDrawing.FontSize"/>).</summary>
internal readonly record struct DiagramPoint(double X, double Y);

/// <summary>The color a stroke is drawn in.</summary>
internal enum DiagramStroke
{
    /// <summary>The text color: outlines, connectors and arrowheads.</summary>
    Text,

    /// <summary>The rule color, as for table borders: the lifelines of a sequence diagram.</summary>
    Rule,
}

/// <summary>One thing a viewer draws; the subclasses are the whole set.</summary>
internal abstract record DiagramShape
{
    /// <summary>The area the shape covers, with the estimated extent for text.</summary>
    public abstract (double Left, double Top, double Right, double Bottom) Bounds();

    /// <summary>The same shape moved by (<paramref name="dx"/>, <paramref name="dy"/>), with coordinates rounded.</summary>
    public abstract DiagramShape MovedBy(double dx, double dy);

    // Two decimals are below what a screen shows and keep the SVG short.
    protected static double Round(double value) => Math.Round(value, 2);

    protected static List<DiagramPoint> Moved(IReadOnlyList<DiagramPoint> points, double dx, double dy) =>
        points.Select(p => new DiagramPoint(Round(p.X + dx), Round(p.Y + dy))).ToList();

    protected static (double, double, double, double) PointBounds(IReadOnlyList<DiagramPoint> points, double pad) =>
        (points.Min(p => p.X) - pad, points.Min(p => p.Y) - pad, points.Max(p => p.X) + pad, points.Max(p => p.Y) + pad);
}

/// <summary>
/// A box filled with the code background and outlined in the text color, with corners rounded by
/// <paramref name="CornerRadius"/>; a radius of half the height makes a stadium, and of half a square side a circle.
/// </summary>
internal sealed record DiagramRectangle(double X, double Y, double Width, double Height, double CornerRadius) : DiagramShape
{
    public override (double Left, double Top, double Right, double Bottom) Bounds() => (X, Y, X + Width, Y + Height);

    public override DiagramShape MovedBy(double dx, double dy) =>
        new DiagramRectangle(Round(X + dx), Round(Y + dy), Round(Width), Round(Height), Round(CornerRadius));
}

/// <summary>
/// A closed shape. An outline is filled with the code background and outlined in the text color; an arrowhead is filled
/// with the text color.
/// </summary>
internal sealed record DiagramPolygon(IReadOnlyList<DiagramPoint> Points, bool IsArrowHead) : DiagramShape
{
    public override (double Left, double Top, double Right, double Bottom) Bounds() => PointBounds(Points, 0);

    public override DiagramShape MovedBy(double dx, double dy) => this with { Points = Moved(Points, dx, dy) };
}

/// <summary>
/// An open line through <paramref name="Points"/>. <paramref name="Dash"/> is the length of each dash and of each gap,
/// or zero for a solid line.
/// </summary>
internal sealed record DiagramLine(IReadOnlyList<DiagramPoint> Points, DiagramStroke Stroke, double Thickness, double Dash) : DiagramShape
{
    public override (double Left, double Top, double Right, double Bottom) Bounds() => PointBounds(Points, Thickness / 2);

    public override DiagramShape MovedBy(double dx, double dy) => this with { Points = Moved(Points, dx, dy) };
}

/// <summary>One line of text in the text color, centered on (<paramref name="X"/>, <paramref name="Y"/>).</summary>
internal sealed record DiagramText(double X, double Y, string Text) : DiagramShape
{
    public override (double Left, double Top, double Right, double Bottom) Bounds()
    {
        var halfWidth = TextMetrics.Width(Text) / 2;
        return (X - halfWidth, Y - TextMetrics.LineHeight / 2, X + halfWidth, Y + TextMetrics.LineHeight / 2);
    }

    public override DiagramShape MovedBy(double dx, double dy) => this with { X = Round(X + dx), Y = Round(Y + dy) };
}
