namespace Polhem.Markdown.Avalonia.Formulas;

/// <summary>
/// A laid-out part of a formula: its width, how far it reaches above and below its baseline, and what it draws, placed
/// relative to its left edge and baseline (y grows downward).
/// </summary>
internal sealed record FormulaBox(double Width, double Ascent, double Descent, IReadOnlyList<FormulaPiece> Pieces)
{
    public static FormulaBox Empty { get; } = new(0, 0, 0, []);

    public double Height => Ascent + Descent;

    /// <summary>The pieces moved so the box's left edge is at <paramref name="x"/> and its baseline at <paramref name="y"/>.</summary>
    public IEnumerable<FormulaPiece> PiecesAt(double x, double y) =>
        x == 0 && y == 0 ? Pieces : Pieces.Select(piece => piece.MovedBy(x, y));
}
