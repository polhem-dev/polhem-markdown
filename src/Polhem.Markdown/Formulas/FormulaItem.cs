namespace Polhem.Markdown.Formulas;

// NOTE: the parts of a formula share this file because they are one closed set that every viewer translates case by
// case, as the diagram shapes are; the viewers' formula coverage tests compare what they drew with the subclasses here.

/// <summary>
/// A part of a math formula parsed from LaTeX. The viewers lay it out themselves: the Blazor viewer writes MathML, the
/// Avalonia viewer measures and draws it.
/// </summary>
internal abstract record FormulaItem;

/// <summary>Parts set one after another on a line.</summary>
internal sealed record FormulaRow(IReadOnlyList<FormulaItem> Items) : FormulaItem;

/// <summary>A letter, a number, an operator or a run of text, set as <see cref="Kind"/> says.</summary>
internal sealed record FormulaSymbol(string Text, FormulaSymbolKind Kind) : FormulaItem;

/// <summary>
/// <see cref="Base"/> with a subscript, a superscript, or both. A large operator, or a name such as <c>lim</c>, sets them
/// below and above it in a formula shown as a block.
/// </summary>
internal sealed record FormulaScripts(FormulaItem Base, FormulaItem? Subscript, FormulaItem? Superscript) : FormulaItem;

/// <summary>A fraction; without a line it is the inside of a binomial coefficient.</summary>
internal sealed record FormulaFraction(FormulaItem Numerator, FormulaItem Denominator, bool HasLine) : FormulaItem;

/// <summary>A square root, or a root of <see cref="Index"/>.</summary>
internal sealed record FormulaRoot(FormulaItem Radicand, FormulaItem? Index) : FormulaItem;

/// <summary>
/// Content between delimiters that grow with it, as <c>\left( … \right)</c> writes them. An empty delimiter is none.
/// </summary>
internal sealed record FormulaFenced(string Open, string Close, FormulaItem Content) : FormulaItem;

/// <summary>
/// A grid of cells, row by row; rows may have fewer cells than others. Cells are centered, or aligned left as the cases
/// of a definition are.
/// </summary>
internal sealed record FormulaMatrix(IReadOnlyList<IReadOnlyList<FormulaItem>> Rows, bool AlignLeft) : FormulaItem;

/// <summary>A mark over <see cref="Base"/>: a hat, a bar, an arrow, a tilde or dots; a stretched one spans all of it.</summary>
internal sealed record FormulaAccent(FormulaItem Base, string Mark, bool Stretches) : FormulaItem;

/// <summary>Horizontal space, in ems; negative space moves the next part back.</summary>
internal sealed record FormulaSpace(double Ems) : FormulaItem;
