namespace Polhem.Markdown.Formulas;

// NOTE: the parts of a formula share this file because they are one closed set that every viewer translates case by
// case, as the diagram shapes are; the viewers' formula coverage tests compare what they drew with the subclasses here.

/// <summary>
/// A part of a math formula parsed from LaTeX. The viewers lay it out themselves: the Blazor viewer writes MathML, the
/// Avalonia viewer measures and draws it.
/// </summary>
internal interface IFormulaItem;

/// <summary>Parts set one after another on a line.</summary>
internal sealed record FormulaRow(IReadOnlyList<IFormulaItem> Items) : IFormulaItem;

/// <summary>A letter, a number, an operator or a run of text, set as <see cref="Kind"/> says.</summary>
internal sealed record FormulaSymbol(string Text, FormulaSymbolKind Kind) : IFormulaItem;

/// <summary>
/// <see cref="Base"/> with a subscript, a superscript, or both. A large operator, or a name such as <c>lim</c>, sets them
/// below and above it in a formula shown as a block.
/// </summary>
internal sealed record FormulaScripts(IFormulaItem Base, IFormulaItem? Subscript, IFormulaItem? Superscript) : IFormulaItem;

/// <summary>A fraction; without a line it is the inside of a binomial coefficient.</summary>
internal sealed record FormulaFraction(IFormulaItem Numerator, IFormulaItem Denominator, bool HasLine) : IFormulaItem;

/// <summary>A square root, or a root of <see cref="Index"/>.</summary>
internal sealed record FormulaRoot(IFormulaItem Radicand, IFormulaItem? Index) : IFormulaItem;

/// <summary>
/// Content between delimiters that grow with it, as <c>\left( … \right)</c> writes them. An empty delimiter is none.
/// </summary>
internal sealed record FormulaFenced(string Open, string Close, IFormulaItem Content) : IFormulaItem;

/// <summary>
/// A grid of cells, row by row; rows may have fewer cells than others. Cells are centered, or aligned left as the cases
/// of a definition are.
/// </summary>
internal sealed record FormulaMatrix(IReadOnlyList<IReadOnlyList<IFormulaItem>> Rows, bool AlignLeft) : IFormulaItem;

/// <summary>A mark over <see cref="Base"/>: a hat, a bar, an arrow, a tilde or dots; a stretched one spans all of it.</summary>
internal sealed record FormulaAccent(IFormulaItem Base, string Mark, bool Stretches) : IFormulaItem;

/// <summary>Horizontal space, in ems; negative space moves the next part back.</summary>
internal sealed record FormulaSpace(double Ems) : IFormulaItem;
