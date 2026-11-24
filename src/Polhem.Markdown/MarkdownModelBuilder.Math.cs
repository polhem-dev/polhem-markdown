using Markdig.Extensions.Mathematics;
using Polhem.Markdown.Formulas;
using Polhem.Markdown.Model;

namespace Polhem.Markdown;

internal sealed partial class MarkdownModelBuilder
{
    /// <summary>A formula shown on its own, or its source as a code block when it is outside the supported subset.</summary>
    private static BlockNode FormulaBlockOrCode(SourceRange range, string latex) =>
        LatexParser.TryParse(latex, out var formula)
            ? new MathBlockNode(range, formula, latex.Trim())
            : new CodeBlockNode(range, "math", latex, []);

    /// <summary>
    /// A formula in a line, or its source as inline code when it is outside the supported subset. GitHub's
    /// <c>$`…`$</c> form keeps its backticks in Markdig's content, so they are taken off here.
    /// </summary>
    private InlineNode FormulaOrCode(MathInline math)
    {
        var latex = math.Content.ToString();
        if (latex is ['`', .., '`'] && latex.Length >= 2)
        {
            latex = latex[1..^1];
        }
        return LatexParser.TryParse(latex, out var formula)
            ? new MathNode(Range(math), formula, latex.Trim())
            : new CodeSpanNode(Range(math), latex);
    }
}
