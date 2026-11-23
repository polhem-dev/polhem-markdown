namespace Polhem.Markdown.Diagrams;

/// <summary>
/// A diagram laid out into shapes with fixed coordinates. Every viewer draws these same shapes at these same positions,
/// scaled down as a whole when the drawing is wider than the space it gets.
/// </summary>
internal sealed record DiagramDrawing(double Width, double Height, IReadOnlyList<DiagramShape> Shapes)
{
    /// <summary>The font size, in drawing units, that the layout assumes for every text.</summary>
    public const double FontSize = 14;

    /// <summary>The stroke thickness of the outline of a box or polygon.</summary>
    public const double OutlineThickness = 1;
}
