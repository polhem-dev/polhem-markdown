using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Polhem.Markdown.Formulas;

namespace Polhem.Markdown.Avalonia.Formulas;

/// <summary>
/// A formula laid out with the font it inherits, so one in a heading takes the heading's size, and drawn in the text
/// color. In a line of text its baseline sits on the line's baseline.
/// </summary>
internal sealed class FormulaView : Control
{
    private readonly IFormulaItem _formula;
    private readonly bool _display;
    private readonly ISet<Type> _laidOut;
    private readonly MarkdownTextBlock? _line;
    private FormulaBox? _box;
    private double _padding;

    /// <param name="formula">The formula.</param>
    /// <param name="display">Whether it is shown on its own rather than in a line.</param>
    /// <param name="laidOut">Collects the formula part types laid out, for the coverage test.</param>
    /// <param name="line">The text a formula in a line belongs to, which makes room for its depth.</param>
    public FormulaView(IFormulaItem formula, bool display, ISet<Type> laidOut, MarkdownTextBlock? line)
    {
        _formula = formula;
        _display = display;
        _laidOut = laidOut;
        _line = line;
    }

    /// <summary>The laid-out formula, laid out when first needed.</summary>
    internal FormulaBox Box => _box ??= LayOut();

    /// <summary>Where the formula's baseline lies below the view's top, for tests.</summary>
    internal double BaselineFromTop => _padding + Box.Ascent;

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextElement.FontSizeProperty || change.Property == TextElement.FontFamilyProperty)
        {
            _box = null;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    // NOTE: the text engine sets an embedded control's bottom on the line's baseline and makes the line taller above it
    // only. In a line the view therefore ends at the formula's baseline, and what reaches below it, a subscript or a
    // denominator, is drawn below the view, in room the text makes for it (see `MarkdownTextBlock.ReserveDepth`).
    protected override Size MeasureOverride(Size availableSize)
    {
        var box = Box;
        return new Size(box.Width + 2 * _padding, _display ? box.Height + 2 * _padding : _padding + box.Ascent);
    }

    public override void Render(DrawingContext context)
    {
        var box = Box;
        var brush = GetValue(TextElement.ForegroundProperty) ?? Brushes.Black;
        var origin = new Vector(_padding, _padding + box.Ascent);
        foreach (var piece in box.Pieces)
        {
            switch (piece.MovedBy(origin.X, origin.Y))
            {
                case FormulaTextPiece text:
                    text.Text.SetForegroundBrush(brush);
                    if (!text.IsScaled)
                    {
                        context.DrawText(text.Text, new Point(text.Origin.X, text.Origin.Y - text.Text.Baseline));
                    }
                    else
                    {
                        // Scale about the baseline start, so the glyph grows from where the layout put it.
                        var matrix = Matrix.CreateTranslation(-text.Origin.X, -text.Origin.Y)
                            * Matrix.CreateScale(text.ScaleX, text.ScaleY)
                            * Matrix.CreateTranslation(text.Origin.X, text.Origin.Y);
                        using (context.PushTransform(matrix))
                        {
                            context.DrawText(text.Text, new Point(text.Origin.X, text.Origin.Y - text.Text.Baseline));
                        }
                    }
                    break;
                case FormulaRulePiece rule:
                    context.FillRectangle(brush, rule.Area);
                    break;
                case FormulaStrokePiece stroke:
                    var geometry = new PolylineGeometry(stroke.Points, isFilled: false);
                    context.DrawGeometry(null, new Pen(brush, stroke.Thickness, lineCap: PenLineCap.Round, lineJoin: PenLineJoin.Round), geometry);
                    break;
            }
        }
    }

    private FormulaBox LayOut()
    {
        var fontSize = GetValue(TextElement.FontSizeProperty);
        _padding = fontSize * 0.05;
        var box = new FormulaLayout(GetValue(TextElement.FontFamilyProperty), fontSize, _laidOut).Layout(_formula, _display);
        if (!_display)
        {
            _line?.ReserveDepth(box.Descent);
        }
        return box;
    }
}
