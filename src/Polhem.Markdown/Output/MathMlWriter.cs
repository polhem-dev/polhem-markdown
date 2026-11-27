using System.Globalization;
using System.Net;
using System.Text;
using Polhem.Markdown.Formulas;

namespace Polhem.Markdown.Output;

/// <summary>
/// Writes a formula as MathML markup, for the Blazor viewer and for <see cref="MarkdownHtml"/>. Blazor's render tree
/// creates every element it does not know as HTML, except SVG, so MathML built there would never be laid out as math;
/// markup, parsed by the browser, gets the MathML namespace.
/// </summary>
/// <remarks>
/// IMPORTANT: this is the only markup the Blazor viewer writes as a string. Element and attribute names and attribute
/// values are constants of this class or numbers, and every text, the LaTeX source included, is HTML-encoded, so nothing
/// from the Markdown can become markup. <c>Render_Math_WritesOnlyMathMl</c> in the Blazor tests and
/// <c>ToFragment_Math_WritesOnlyMathMl</c> check the elements and attributes written.
/// </remarks>
internal static class MathMlWriter
{
    private const string CloseOperator = "</mo>";

    public static string Write(IFormulaItem formula, string source, bool display, ISet<Type> written)
    {
        var markup = new StringBuilder();
        markup.Append(display ? "<math display=\"block\">" : "<math>").Append("<semantics>");
        Part(markup, formula, written);
        markup.Append("<annotation encoding=\"application/x-tex\">").Append(WebUtility.HtmlEncode(source)).Append("</annotation>");
        markup.Append("</semantics></math>");
        return markup.ToString();
    }

    private static void Part(StringBuilder markup, IFormulaItem item, ISet<Type> written)
    {
        switch (item)
        {
            case FormulaRow row:
                markup.Append("<mrow>");
                foreach (var child in row.Items)
                {
                    Part(markup, child, written);
                }
                markup.Append("</mrow>");
                break;
            case FormulaSymbol symbol:
                Symbol(markup, symbol);
                break;
            case FormulaScripts scripts:
                Scripts(markup, scripts, written);
                break;
            case FormulaFraction fraction:
                markup.Append(fraction.HasLine ? "<mfrac>" : "<mfrac linethickness=\"0\">");
                Part(markup, fraction.Numerator, written);
                Part(markup, fraction.Denominator, written);
                markup.Append("</mfrac>");
                break;
            case FormulaRoot { Index: null } root:
                markup.Append("<msqrt>");
                Part(markup, root.Radicand, written);
                markup.Append("</msqrt>");
                break;
            case FormulaRoot { Index: { } index } root:
                markup.Append("<mroot>");
                Part(markup, root.Radicand, written);
                Part(markup, index, written);
                markup.Append("</mroot>");
                break;
            case FormulaFenced fenced:
                markup.Append("<mrow>");
                Fence(markup, fenced.Open);
                Part(markup, fenced.Content, written);
                Fence(markup, fenced.Close);
                markup.Append("</mrow>");
                break;
            case FormulaMatrix matrix:
                Matrix(markup, matrix, written);
                break;
            case FormulaAccent accent:
                markup.Append("<mover accent=\"true\">");
                Part(markup, accent.Base, written);
                markup.Append(accent.Stretches ? "<mo stretchy=\"true\">" : "<mo stretchy=\"false\">")
                    .Append(WebUtility.HtmlEncode(accent.Mark)).Append("</mo></mover>");
                break;
            case FormulaSpace space:
                var ems = Math.Abs(space.Ems).ToString("0.####", CultureInfo.InvariantCulture);
                // A MathML space cannot be narrower than nothing, so negative space is a negative margin.
                markup.Append(space.Ems >= 0 ? "<mspace width=\"" + ems + "em\"></mspace>" : "<mspace style=\"margin-inline-start:-" + ems + "em\"></mspace>");
                break;
            default:
                return;
        }
        written.Add(item.GetType());
    }

    private static void Symbol(StringBuilder markup, FormulaSymbol symbol)
    {
        var (open, close) = symbol.Kind switch
        {
            FormulaSymbolKind.Identifier => ("<mi>", "</mi>"),
            FormulaSymbolKind.Name => ("<mi mathvariant=\"normal\">", "</mi>"),
            FormulaSymbolKind.Number => ("<mn>", "</mn>"),
            FormulaSymbolKind.Text => ("<mtext>", "</mtext>"),
            FormulaSymbolKind.LimitName => ("<mo movablelimits=\"true\">", CloseOperator),
            FormulaSymbolKind.LargeOperator => ("<mo largeop=\"true\" movablelimits=\"true\">", CloseOperator),
            FormulaSymbolKind.Integral => ("<mo largeop=\"true\">", CloseOperator),
            // NOTE: MathML stretches a bracket written as an operator to its row; only `\left … \right` and the
            // delimiters of a matrix grow, as in LaTeX, so a bracket written on its own keeps its size.
            _ when symbol.Text is "(" or ")" or "[" or "]" or "{" or "}" or "|" or "\u2016" or "\u27E8" or "\u27E9" or "\u230A"
                or "\u230B" or "\u2308" or "\u2309" => ("<mo stretchy=\"false\">", CloseOperator),
            _ => ("<mo>", CloseOperator),
        };

        // HTML would collapse a space at the end of text, which `\text{if }` means to keep.
        var text = symbol.Kind == FormulaSymbolKind.Text ? symbol.Text.Replace(' ', '\u00A0') : symbol.Text;
        markup.Append(open).Append(WebUtility.HtmlEncode(text)).Append(close);
    }

    /// <summary>
    /// Writes scripts: below and above a large operator or a name such as <c>lim</c>, which the browser moves to the side
    /// in a formula within a line, and beside anything else.
    /// </summary>
    private static void Scripts(StringBuilder markup, FormulaScripts scripts, ISet<Type> written)
    {
        var limits = scripts.Base is FormulaSymbol { Kind: FormulaSymbolKind.LargeOperator or FormulaSymbolKind.LimitName };
        var element = (scripts.Subscript, scripts.Superscript) switch
        {
            (not null, not null) => limits ? "munderover" : "msubsup",
            (not null, null) => limits ? "munder" : "msub",
            _ => limits ? "mover" : "msup",
        };
        markup.Append('<').Append(element).Append('>');
        Part(markup, scripts.Base, written);
        if (scripts.Subscript is { } subscript)
        {
            Part(markup, subscript, written);
        }
        if (scripts.Superscript is { } superscript)
        {
            Part(markup, superscript, written);
        }
        markup.Append("</").Append(element).Append('>');
    }

    private static void Fence(StringBuilder markup, string delimiter)
    {
        if (delimiter.Length > 0)
        {
            markup.Append("<mo fence=\"true\" stretchy=\"true\">").Append(WebUtility.HtmlEncode(delimiter)).Append(CloseOperator);
        }
    }

    private static void Matrix(StringBuilder markup, FormulaMatrix matrix, ISet<Type> written)
    {
        var cell = matrix.AlignLeft ? "<mtd class=\"pmd-math-left\">" : "<mtd>";
        markup.Append("<mtable>");
        foreach (var row in matrix.Rows)
        {
            markup.Append("<mtr>");
            foreach (var item in row)
            {
                markup.Append(cell);
                Part(markup, item, written);
                markup.Append("</mtd>");
            }
            markup.Append("</mtr>");
        }
        markup.Append("</mtable>");
    }
}
