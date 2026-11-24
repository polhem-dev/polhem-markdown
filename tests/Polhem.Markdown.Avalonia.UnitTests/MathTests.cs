using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Polhem.Markdown.Avalonia.Formulas;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>Math, which the Avalonia viewer lays out with its own font.</summary>
[Collection(HeadlessCollection.Name)]
public class MathTests(HeadlessSession session)
{
    private static FormulaTextPiece Glyph(FormulaView view, string text) =>
        view.Box.Pieces.OfType<FormulaTextPiece>().Single(piece => piece.Content == text);

    [Fact(DisplayName = "Display math is a formula centered on its own")]
    public Task Render_DisplayMath_IsCentered() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("$$\n\\frac{a}{b}\n$$");

        var scroller = Assert.IsType<ScrollViewer>(Assert.Single(Blocks(viewer)));
        var view = Assert.IsType<FormulaView>(scroller.Content);
        Assert.Equal(HorizontalAlignment.Center, view.HorizontalAlignment);
        Assert.True(view.Bounds.Width > 0);
    });

    [Fact(DisplayName = "Inline math is a formula embedded in the line, taking one character")]
    public Task Render_InlineMath_IsEmbedded() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("Area $\\pi r^2$ here.");

        var text = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer)));
        Assert.IsType<FormulaView>(Assert.Single(text.Inlines!.OfType<InlineUIContainer>()).Child);
    });

    [Theory(DisplayName = "An inline formula's baseline sits on the line's, and a deep one makes room below the text")]
    [InlineData("M $x$ M")]
    [InlineData("M $\\frac{a}{b}$ M")]
    [InlineData("M $y_{jjj}$ M")]
    public Task Render_InlineFormula_SitsOnBaseline(string markdown) => session.RunAsync(() =>
    {
        var (_, viewer) = Show(markdown);

        var text = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer)));
        var view = Descendants<FormulaView>(viewer).Single();
        Assert.Equal(text.TextLayout.TextLines[0].Baseline, view.Bounds.Top + view.BaselineFromTop, precision: 0);
        Assert.True(text.Padding.Bottom >= view.Box.Descent - text.FontSize * 0.22 - 0.01);
    });

    [Fact(DisplayName = "A fraction sets its numerator above the bar and its denominator below")]
    public Task Layout_Fraction_StacksParts() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("$$\\frac{a}{b}$$");
        var view = Descendants<FormulaView>(viewer).Single();

        var bar = Assert.Single(view.Box.Pieces.OfType<FormulaRulePiece>()).Area;
        Assert.True(Glyph(view, "a").Origin.Y < bar.Top);
        Assert.True(Glyph(view, "b").Origin.Y > bar.Bottom);
    });

    [Fact(DisplayName = "A superscript is raised and smaller, a subscript lowered")]
    public Task Layout_Scripts_AreRaisedAndLowered() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("$x_i^2$");
        var view = Descendants<FormulaView>(viewer).Single();

        var x = Glyph(view, "x");
        Assert.True(Glyph(view, "2").Origin.Y < x.Origin.Y);
        Assert.True(Glyph(view, "i").Origin.Y > x.Origin.Y);
        Assert.True(Glyph(view, "2").Text.Height < x.Text.Height);
    });

    [Fact(DisplayName = "Limits of a large operator go below and above it on its own, and beside it in a line")]
    public Task Layout_Limits_DependOnDisplay() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("$$\\sum_{k}^{n}$$\n\n$\\sum_{k}^{n}$");
        var views = Descendants<FormulaView>(viewer).ToList();

        var display = views[0];
        Assert.True(Glyph(display, "n").Origin.X < Glyph(display, "∑").Origin.X + Glyph(display, "∑").Text.Width);
        var inline = views[1];
        Assert.True(Glyph(inline, "n").Origin.X >= Glyph(inline, "∑").Origin.X + Glyph(inline, "∑").Text.Width);
    });

    [Fact(DisplayName = "A delimiter grows with a tall content")]
    public Task Layout_Delimiter_Grows() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("$$\\left( \\frac{\\frac{a}{b}}{\\frac{c}{d}} \\right)$$");
        var view = Descendants<FormulaView>(viewer).Single();

        Assert.True(Glyph(view, "(").ScaleY > 1.5);
    });

    [Fact(DisplayName = "Math outside the supported subset shows its source as code")]
    public Task Render_UnsupportedMath_IsCode() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("$\\mathbf{x}$");

        Assert.Empty(Descendants<FormulaView>(viewer));
        Assert.Equal("\\mathbf{x}", Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer))).Inlines!.Text);
    });
}
