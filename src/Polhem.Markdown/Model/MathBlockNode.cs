using Polhem.Markdown.Formulas;

namespace Polhem.Markdown.Model;

/// <summary>
/// A formula shown on its own, centered: <c>$$…$$</c>, or a <c>math</c> code block. A formula outside the supported
/// subset is a code block instead.
/// </summary>
internal sealed record MathBlockNode(SourceRange Range, IFormulaItem Formula, string Source) : BlockNode(Range);
