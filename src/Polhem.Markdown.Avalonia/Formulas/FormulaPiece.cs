using Avalonia;
using Avalonia.Media;

namespace Polhem.Markdown.Avalonia.Formulas;

// NOTE: the pieces share this file because they are one closed set that `FormulaView` draws case by case.

/// <summary>One thing a laid-out formula draws, placed relative to the formula's left edge and baseline.</summary>
internal abstract record FormulaPiece
{
    /// <summary>The same piece moved by (<paramref name="dx"/>, <paramref name="dy"/>).</summary>
    public abstract FormulaPiece MovedBy(double dx, double dy);
}

/// <summary>
/// <see cref="Content"/>, shaped as <see cref="Text"/>, whose baseline starts at <see cref="Origin"/>, scaled by
/// <see cref="ScaleX"/> and <see cref="ScaleY"/> about its baseline start, as a delimiter grows with its content.
/// </summary>
internal sealed record FormulaTextPiece(Point Origin, string Content, FormattedText Text, double ScaleX, double ScaleY) : FormulaPiece
{
    public override FormulaPiece MovedBy(double dx, double dy) => this with { Origin = new Point(Origin.X + dx, Origin.Y + dy) };
}

/// <summary>A filled rectangle: a fraction bar, the top of a root, an overline.</summary>
internal sealed record FormulaRulePiece(Rect Area) : FormulaPiece
{
    public override FormulaPiece MovedBy(double dx, double dy) => this with { Area = Area.Translate(new Vector(dx, dy)) };
}

/// <summary>A stroked line through <see cref="Points"/>: the sign of a root.</summary>
internal sealed record FormulaStrokePiece(IReadOnlyList<Point> Points, double Thickness) : FormulaPiece
{
    public override FormulaPiece MovedBy(double dx, double dy) =>
        this with { Points = [.. Points.Select(point => new Point(point.X + dx, point.Y + dy))] };
}
