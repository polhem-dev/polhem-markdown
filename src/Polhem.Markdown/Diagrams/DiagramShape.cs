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
internal abstract record DiagramShape;

/// <summary>
/// A box filled with the code background and outlined in the text color, with corners rounded by
/// <paramref name="CornerRadius"/>; a radius of half the height makes a stadium, and of half a square side a circle.
/// </summary>
internal sealed record DiagramRectangle(double X, double Y, double Width, double Height, double CornerRadius) : DiagramShape;

/// <summary>
/// A closed shape. An outline is filled with the code background and outlined in the text color; an arrowhead is filled
/// with the text color.
/// </summary>
internal sealed record DiagramPolygon(IReadOnlyList<DiagramPoint> Points, bool IsArrowHead) : DiagramShape;

/// <summary>
/// An open line through <paramref name="Points"/>. <paramref name="Dash"/> is the length of each dash and of each gap,
/// or zero for a solid line.
/// </summary>
internal sealed record DiagramLine(IReadOnlyList<DiagramPoint> Points, DiagramStroke Stroke, double Thickness, double Dash) : DiagramShape;

/// <summary>One line of text in the text color, centered on (<paramref name="X"/>, <paramref name="Y"/>).</summary>
internal sealed record DiagramText(double X, double Y, string Text) : DiagramShape;
