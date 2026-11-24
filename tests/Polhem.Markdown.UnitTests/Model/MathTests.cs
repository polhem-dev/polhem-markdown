using Polhem.Markdown.Formulas;
using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

/// <summary>Math written as on GitHub: <c>$…$</c>, <c>$$…$$</c> and <c>math</c> code blocks.</summary>
public class MathTests
{
    private static FormulaItem Formula(string latex)
    {
        Assert.True(LatexParser.TryParse(latex, out var formula), latex);
        return formula;
    }

    private static T Only<T>(FormulaItem formula) where T : FormulaItem =>
        Assert.IsType<T>(Assert.Single(Assert.IsType<FormulaRow>(formula).Items));

    [Theory(DisplayName = "Dollar signs in prose, such as prices, stay text")]
    [InlineData("Costs $5 and $10 today.")]
    [InlineData("It is \\$x$ here.")]
    [InlineData("$x$5")]
    [InlineData("I have $ 5 and you $ 6")]
    [InlineData("costs 5 $ and 10 $ total")]
    [InlineData("| Item | Price ($) | Tax ($) |\n|---|---|---|\n| pen | 2 | 1 |")]
    [InlineData("| $x | y$ |\n|---|---|")]
    public void Build_DollarsInProse_StayText(string markdown)
    {
        var document = Parse(markdown);

        Assert.DoesNotContain(AllNodes(document), node => node is MathNode or MathBlockNode);
        Assert.Contains("$", string.Concat(AllNodes(document).OfType<TextNode>().Select(text => text.Text)), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "A table keeps its cells when a cell holds dollar signs, and math within one cell still renders")]
    public void Build_TableWithDollars_KeepsCells()
    {
        var table = Single<TableNode>(Parse("| Item | Price ($) | Tax ($) |\n|---|---|---|\n| $\\alpha$ | $x^2$ | 1 |"));

        Assert.Equal(3, table.HeaderRows[0].Cells.Count);
        Assert.Single(table.Rows[0].Cells[0].Inlines.OfType<MathNode>());
    }

    [Fact(DisplayName = "Outside a table, math may hold a vertical bar")]
    public void Build_AbsoluteValue_IsMath()
    {
        Assert.Equal("|x|", Assert.Single(AllNodes(Parse("so $|x| < 1$ holds")).OfType<MathNode>()).Source[..3]);
    }

    [Fact(DisplayName = "Inline math becomes a formula in the line, with its source")]
    public void Build_InlineMath_IsFormula()
    {
        var paragraph = Single<ParagraphNode>(Parse("Area $\\pi r^2$ here."));

        var math = Assert.IsType<MathNode>(paragraph.Inlines[1]);
        Assert.Equal("\\pi r^2", math.Source);
        Assert.Equal(new SourceRange(5, 9), math.Range);
    }

    [Fact(DisplayName = "GitHub's $`…`$ form is inline math without its backticks")]
    public void Build_BacktickMath_IsFormula()
    {
        var math = Assert.Single(AllNodes(Parse("See $`y_1`$.")).OfType<MathNode>());

        Assert.Equal("y_1", math.Source);
    }

    [Theory(DisplayName = "Display math, a math code block, and $$…$$ alone in a paragraph are formula blocks")]
    [InlineData("$$\nE = mc^2\n$$")]
    [InlineData("```math\nE = mc^2\n```")]
    [InlineData("$$E = mc^2$$")]
    public void Build_DisplayMath_IsBlock(string markdown)
    {
        Assert.Equal("E = mc^2", Single<MathBlockNode>(Parse(markdown)).Source);
    }

    [Fact(DisplayName = "$$…$$ within a line of text stays in the line")]
    public void Build_DisplayDelimitersInLine_StayInline()
    {
        Assert.Single(Single<ParagraphNode>(Parse("so $$x$$ holds")).Inlines.OfType<MathNode>());
    }

    [Theory(DisplayName = "Math outside the supported subset shows its source as code, all or nothing")]
    [InlineData("$\\mathbf{x} + 1$")]
    [InlineData("$\\unknown$")]
    [InlineData("$x^{$")]
    public void Build_UnsupportedInlineMath_IsCode(string markdown)
    {
        var paragraph = Single<ParagraphNode>(Parse(markdown));

        Assert.Equal(markdown[1..^1], Assert.IsType<CodeSpanNode>(Assert.Single(paragraph.Inlines)).Code);
    }

    [Fact(DisplayName = "A math block outside the subset shows as a math code block")]
    public void Build_UnsupportedMathBlock_IsCode()
    {
        var code = Single<CodeBlockNode>(Parse("$$\n\\begin{align} a \\end{align}\n$$"));

        Assert.Equal("math", code.Language);
        Assert.Contains("\\begin{align}", code.Code, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Letters are identifiers, digits numbers, and a minus sign a true minus")]
    public void Parse_Symbols_HaveKinds()
    {
        var items = Assert.IsType<FormulaRow>(Formula("x - 12.5")).Items.Cast<FormulaSymbol>().ToList();

        Assert.Equal([("x", FormulaSymbolKind.Identifier), ("\u2212", FormulaSymbolKind.Operator), ("12.5", FormulaSymbolKind.Number)],
            items.Select(symbol => (symbol.Text, symbol.Kind)));
    }

    [Theory(DisplayName = "Commands for symbols become the symbol, set by its kind")]
    [InlineData("\\alpha", "α", FormulaSymbolKind.Identifier)]
    [InlineData("\\Gamma", "Γ", FormulaSymbolKind.Name)]
    [InlineData("\\sin", "sin", FormulaSymbolKind.Name)]
    [InlineData("\\lim", "lim", FormulaSymbolKind.LimitName)]
    [InlineData("\\sum", "∑", FormulaSymbolKind.LargeOperator)]
    [InlineData("\\int", "∫", FormulaSymbolKind.Integral)]
    [InlineData("\\leq", "≤", FormulaSymbolKind.Operator)]
    [InlineData("\\infty", "∞", FormulaSymbolKind.Name)]
    [InlineData("\\text{if } ", "if ", FormulaSymbolKind.Text)]
    [InlineData("\\operatorname{rank}", "rank", FormulaSymbolKind.Name)]
    internal void Parse_SymbolCommand_IsSymbol(string latex, string text, FormulaSymbolKind kind)
    {
        Assert.Equal(new FormulaSymbol(text, kind), Only<FormulaSymbol>(Formula(latex)));
    }

    [Fact(DisplayName = "Scripts attach to their base, a single digit or a group at a time, and primes raise")]
    public void Parse_Scripts_AttachToBase()
    {
        var scripts = Only<FormulaScripts>(Formula("x_{i}^2"));
        Assert.Equal(new FormulaSymbol("x", FormulaSymbolKind.Identifier), scripts.Base);
        Assert.Equal(new FormulaSymbol("2", FormulaSymbolKind.Number), scripts.Superscript);

        // As in LaTeX, `x^23` raises the 2 only.
        var digits = Assert.IsType<FormulaRow>(Formula("x^23")).Items;
        Assert.Equal(new FormulaSymbol("2", FormulaSymbolKind.Number), Assert.IsType<FormulaScripts>(digits[0]).Superscript);
        Assert.Equal(new FormulaSymbol("3", FormulaSymbolKind.Number), digits[1]);

        var prime = Only<FormulaScripts>(Formula("f'"));
        Assert.Equal(new FormulaSymbol("\u2032", FormulaSymbolKind.Operator), prime.Superscript);
    }

    [Fact(DisplayName = "Fractions, binomials and roots keep their parts")]
    public void Parse_FractionsAndRoots_KeepParts()
    {
        Assert.True(Only<FormulaFraction>(Formula("\\frac{a}{b}")).HasLine);
        Assert.Equal(new FormulaSymbol("1", FormulaSymbolKind.Number), Only<FormulaFraction>(Formula("\\frac12")).Numerator);
        var binomial = Only<FormulaFenced>(Formula("\\binom{n}{k}"));
        Assert.False(Assert.IsType<FormulaFraction>(binomial.Content).HasLine);
        Assert.NotNull(Only<FormulaRoot>(Formula("\\sqrt[3]{x}")).Index);
        Assert.Null(Only<FormulaRoot>(Formula("\\sqrt x")).Index);
    }

    [Fact(DisplayName = "Delimiters written with \\left and \\right enclose their content")]
    public void Parse_LeftRight_Encloses()
    {
        var fenced = Only<FormulaFenced>(Formula("\\left\\langle x \\right."));

        Assert.Equal(("\u27E8", ""), (fenced.Open, fenced.Close));
    }

    [Fact(DisplayName = "A matrix keeps its rows and cells, between the delimiters its environment names")]
    public void Parse_Matrix_KeepsCells()
    {
        var fenced = Only<FormulaFenced>(Formula("\\begin{pmatrix} 1 & 2 \\\\ 3 & 4 \\\\ \\end{pmatrix}"));
        var matrix = Assert.IsType<FormulaMatrix>(fenced.Content);

        Assert.Equal(("(", ")"), (fenced.Open, fenced.Close));
        Assert.Equal([2, 2], matrix.Rows.Select(row => row.Count));
        Assert.True(Assert.IsType<FormulaMatrix>(Only<FormulaFenced>(Formula("\\begin{cases} 1 & x > 0 \\\\ 0 & \\text{else} \\end{cases}")).Content).AlignLeft);
    }

    [Fact(DisplayName = "A character outside the Basic Multilingual Plane stays whole")]
    public void Parse_SurrogatePair_StaysWhole()
    {
        var items = Assert.IsType<FormulaRow>(Formula("\U0001D53D + \U0001F600")).Items.Cast<FormulaSymbol>().ToList();

        Assert.Equal(new FormulaSymbol("\U0001D53D", FormulaSymbolKind.Identifier), items[0]);
        Assert.Equal(new FormulaSymbol("\U0001F600", FormulaSymbolKind.Operator), items[2]);
        Assert.False(LatexParser.TryParse("x\uD835", out _));
    }

    [Fact(DisplayName = "Accents and spaces are kept")]
    public void Parse_AccentsAndSpaces_AreKept()
    {
        Assert.Equal("\u2192", Only<FormulaAccent>(Formula("\\vec{v}")).Mark);
        Assert.True(Only<FormulaAccent>(Formula("\\overline{AB}")).Stretches);
        Assert.Equal(-3 / 18.0, Only<FormulaSpace>(Formula("\\!")).Ems);
    }

    [Theory(DisplayName = "Malformed or unsupported LaTeX is not parsed, and never fails")]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("x^")]
    [InlineData("x^a^b")]
    [InlineData("\\")]
    [InlineData("\\left( x")]
    [InlineData("x \\right)")]
    [InlineData("\\begin{pmatrix} a")]
    [InlineData("\\begin{pmatrix} a \\end{bmatrix}")]
    [InlineData("a & b")]
    [InlineData("a \\\\ b")]
    [InlineData("\\text{a")]
    [InlineData("\\sqrt[3")]
    [InlineData("#")]
    [InlineData("   ")]
    public void Parse_Malformed_IsRefused(string latex)
    {
        Assert.False(LatexParser.TryParse(latex, out _));
    }

    [Fact(DisplayName = "Nesting past the limit is refused instead of overflowing the stack")]
    public void Parse_DeepNesting_IsRefused()
    {
        Assert.False(LatexParser.TryParse(new string('{', 100_000) + new string('}', 100_000), out _));
        Assert.False(LatexParser.TryParse(string.Concat(Enumerable.Repeat("\\frac", 100_000)) + "12", out _));
        Assert.True(LatexParser.TryParse(new string('{', 20) + "x" + new string('}', 20), out _));
    }

    [Fact(DisplayName = "A source at the length limit is parsed, and a longer one shows as written")]
    public void Parse_LengthLimit_Applies()
    {
        const string Unit = "x^2+\\frac{a}{b}";
        var atLimit = string.Concat(Enumerable.Repeat(Unit, LatexParser.MaxLength / Unit.Length));

        Assert.True(LatexParser.TryParse(atLimit, out _));
        Assert.False(LatexParser.TryParse(atLimit + new string('x', LatexParser.MaxLength), out _));
    }
}
