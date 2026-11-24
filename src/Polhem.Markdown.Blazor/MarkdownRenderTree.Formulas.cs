using Polhem.Markdown.Formulas;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

internal sealed partial class MarkdownRenderTree
{
    private readonly HashSet<Type> _renderedFormulas = [];

    /// <summary>The formula part types written so far.</summary>
    public IReadOnlySet<Type> RenderedFormulaTypes => _renderedFormulas;

    /// <summary>
    /// Writes a formula as MathML, which the browser lays out itself with no script (see <see cref="MathMlWriter"/>). Its
    /// LaTeX source goes along as an annotation, which is not shown.
    /// </summary>
    private void Formula(FormulaItem formula, string source, bool display) =>
        _builder.AddMarkupContent(0, MathMlWriter.Write(formula, source, display, _renderedFormulas));

    private void MathBlock(MathBlockNode math)
    {
        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, "pmd-math");
        _builder.OpenRegion(2);
        Formula(math.Formula, math.Source, display: true);
        _builder.CloseRegion();
        _builder.CloseElement();
    }
}
