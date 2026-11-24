using Polhem.Markdown.Formulas;

namespace Polhem.Markdown.Model;

/// <summary>
/// A formula in a line of text, written <c>$…$</c>. <see cref="Source"/> is its LaTeX, which a viewer may give as the
/// formula's text, for copying and for screen readers. A formula outside the supported subset is inline code instead.
/// </summary>
internal sealed record MathNode(SourceRange Range, IFormulaItem Formula, string Source) : InlineNode(Range);
