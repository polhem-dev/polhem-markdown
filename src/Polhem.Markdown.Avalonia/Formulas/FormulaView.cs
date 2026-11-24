using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Polhem.Markdown.Formulas;

namespace Polhem.Markdown.Avalonia.Formulas;

/// <summary>
/// A formula laid out with the viewer's font and drawn in its text color. In a line of text its baseline sits on the
/// line's baseline.
/// </summary>
internal sealed class FormulaView : Control
{
    private readonly FormulaBox _box;
    private readonly double _padding;
    private readonly double _height;

    public FormulaView(MarkdownViewer viewer, FormulaItem formula, bool display, ISet<Type> laidOut)
    {
        var fontSize = viewer.GetValue(TextElement.FontSizeProperty);
        _box = new FormulaLayout(viewer.GetValue(TextElement.FontFamilyProperty), fontSize, laidOut).Layout(formula, display);
        _padding = fontSize * 0.05;

        // NOTE: the text engine sets an embedded control's bottom on the line's baseline and makes the line taller
        // above it only. In a line the view therefore ends at the formula's baseline, and what reaches below it, a
        // subscript or a denominator, is drawn below the view, in the room the text keeps for its own descenders.
        _height = display ? _box.Height + 2 * _padding : _padding + _box.Ascent;
    }

    /// <summary>Where the formula's baseline lies below the view's top, for tests.</summary>
    internal double BaselineFromTop => _padding + _box.Ascent;

    /// <summary>The laid-out formula, for tests.</summary>
    internal FormulaBox Box => _box;

    protected override Size MeasureOverride(Size availableSize) => new(_box.Width + 2 * _padding, _height);

    public override void Render(DrawingContext context)
    {
        var brush = GetValue(TextElement.ForegroundProperty) ?? Brushes.Black;
        var origin = new Vector(_padding, _padding + _box.Ascent);
        foreach (var piece in _box.Pieces)
        {
            switch (piece.MovedBy(origin.X, origin.Y))
            {
                case FormulaTextPiece text:
                    text.Text.SetForegroundBrush(brush);
                    var top = new Point(text.Origin.X, text.Origin.Y - text.Text.Baseline * text.ScaleY);
                    if (text.ScaleX == 1 && text.ScaleY == 1)
                    {
                        context.DrawText(text.Text, top);
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
}
