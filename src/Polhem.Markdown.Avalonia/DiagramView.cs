using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Polhem.Markdown.Diagrams;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Draws the shapes of a laid-out diagram at the drawing's own size; the renderer wraps it in a
/// <see cref="Viewbox"/> that scales it down when it is wider than the viewer.
/// </summary>
internal sealed class DiagramView : Control
{
    public static readonly StyledProperty<IBrush?> TextBrushProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(TextBrush));

    public static readonly StyledProperty<IBrush?> FillBrushProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(FillBrush));

    public static readonly StyledProperty<IBrush?> RuleBrushProperty =
        AvaloniaProperty.Register<DiagramView, IBrush?>(nameof(RuleBrush));

    private readonly DiagramDrawing _drawing;
    private readonly List<(DiagramShape Shape, Geometry? Geometry)> _shapes = [];

    static DiagramView()
    {
        AffectsRender<DiagramView>(TextBrushProperty, FillBrushProperty, RuleBrushProperty, TextElement.FontFamilyProperty);
    }

    public DiagramView(MarkdownViewer viewer, DiagramNode node, ISet<Type> drawnShapes)
    {
        _drawing = node.Drawing;
        Width = _drawing.Width;
        Height = _drawing.Height;
        Bind(TextBrushProperty, viewer.GetObservable(TextElement.ForegroundProperty));
        Bind(FillBrushProperty, viewer.GetObservable(MarkdownViewer.CodeBackgroundProperty));
        Bind(RuleBrushProperty, viewer.GetObservable(MarkdownViewer.RuleBrushProperty));

        foreach (var shape in _drawing.Shapes)
        {
            Geometry? geometry;
            switch (shape)
            {
                case DiagramRectangle:
                case DiagramText:
                    geometry = null;
                    break;
                case DiagramPolygon polygon:
                    geometry = new PolylineGeometry(polygon.Points.Select(Point), isFilled: true);
                    break;
                case DiagramLine line:
                    geometry = new PolylineGeometry(line.Points.Select(Point), isFilled: false);
                    break;
                default:
                    // NOTE: a shape type is recorded only when a case above handles it, so the shape coverage test fails
                    // for a type added to the drawing that this view does not draw.
                    continue;
            }
            _shapes.Add((shape, geometry));
            drawnShapes.Add(shape.GetType());
        }
    }

    public IBrush? TextBrush
    {
        get => GetValue(TextBrushProperty);
        set => SetValue(TextBrushProperty, value);
    }

    public IBrush? FillBrush
    {
        get => GetValue(FillBrushProperty);
        set => SetValue(FillBrushProperty, value);
    }

    public IBrush? RuleBrush
    {
        get => GetValue(RuleBrushProperty);
        set => SetValue(RuleBrushProperty, value);
    }

    /// <summary>The text of the drawing, in drawing order, for tests.</summary>
    internal IEnumerable<string> Texts => _drawing.Shapes.OfType<DiagramText>().Select(text => text.Text);

    public override void Render(DrawingContext context)
    {
        var textBrush = TextBrush ?? Brushes.Black;
        var outline = new Pen(textBrush, DiagramDrawing.OutlineThickness);
        var typeface = new Typeface(GetValue(TextElement.FontFamilyProperty));

        foreach (var (shape, geometry) in _shapes)
        {
            switch (shape)
            {
                case DiagramRectangle rectangle:
                    context.DrawRectangle(FillBrush, outline, new Rect(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height),
                        rectangle.CornerRadius, rectangle.CornerRadius);
                    break;
                case DiagramPolygon polygon:
                    context.DrawGeometry(polygon.IsArrowHead ? textBrush : FillBrush, polygon.IsArrowHead ? null : outline, geometry!);
                    break;
                case DiagramLine line:
                    context.DrawGeometry(null, LinePen(line, textBrush), geometry!);
                    break;
                case DiagramText text:
                    var formatted = new FormattedText(text.Text, CultureInfo.CurrentCulture, global::Avalonia.Media.FlowDirection.LeftToRight, typeface,
                        DiagramDrawing.FontSize, textBrush);
                    context.DrawText(formatted, new Point(text.X - formatted.Width / 2, text.Y - formatted.Height / 2));
                    break;
            }
        }
    }

    private Pen LinePen(DiagramLine line, IBrush textBrush)
    {
        var brush = line.Stroke == DiagramStroke.Rule ? RuleBrush ?? textBrush : textBrush;

        // Avalonia measures dashes in multiples of the pen thickness; the drawing gives them in drawing units.
        var dashes = line.Dash > 0 ? new DashStyle([line.Dash / line.Thickness, line.Dash / line.Thickness], 0) : null;
        return new Pen(brush, line.Thickness, dashes, PenLineCap.Flat, PenLineJoin.Round);
    }

    private static Point Point(DiagramPoint point) => new(point.X, point.Y);
}
