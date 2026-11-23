using System.Globalization;
using Polhem.Markdown.Diagrams;

namespace Polhem.Markdown.Blazor;

internal sealed partial class MarkdownRenderTree
{
    /// <summary>
    /// Writes a diagram as inline SVG at the drawing's own size; the stylesheet scales it down when it is wider than the
    /// viewer. Colors come from classes, so they follow the <c>--pmd-*</c> variables and the text color.
    /// </summary>
    private void Diagram(DiagramDrawing drawing)
    {
        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, "pmd-diagram");
        _builder.OpenElement(2, "svg");
        _builder.AddAttribute(3, "viewBox", "0 0 " + Number(drawing.Width) + " " + Number(drawing.Height));
        _builder.AddAttribute(4, "width", Number(drawing.Width));
        _builder.AddAttribute(5, "height", Number(drawing.Height));
        _builder.AddAttribute(6, "font-size", Number(DiagramDrawing.FontSize));
        _builder.AddAttribute(7, "stroke-width", Number(DiagramDrawing.OutlineThickness));
        foreach (var shape in drawing.Shapes)
        {
            _builder.OpenRegion(8);
            Shape(shape);
            _builder.CloseRegion();
        }
        _builder.CloseElement();
        _builder.CloseElement();
    }

    private void Shape(DiagramShape shape)
    {
        switch (shape)
        {
            case DiagramRectangle rectangle:
                _builder.OpenElement(0, "rect");
                _builder.AddAttribute(1, ClassAttribute, "pmd-d-box");
                _builder.AddAttribute(2, "x", Number(rectangle.X));
                _builder.AddAttribute(3, "y", Number(rectangle.Y));
                _builder.AddAttribute(4, "width", Number(rectangle.Width));
                _builder.AddAttribute(5, "height", Number(rectangle.Height));
                if (rectangle.CornerRadius > 0)
                {
                    _builder.AddAttribute(6, "rx", Number(rectangle.CornerRadius));
                }
                _builder.CloseElement();
                break;
            case DiagramPolygon polygon:
                _builder.OpenElement(7, "polygon");
                _builder.AddAttribute(8, ClassAttribute, polygon.IsArrowHead ? "pmd-d-arrow" : "pmd-d-box");
                _builder.AddAttribute(9, "points", Points(polygon.Points));
                _builder.CloseElement();
                break;
            case DiagramLine line:
                _builder.OpenElement(10, "polyline");
                _builder.AddAttribute(11, ClassAttribute, line.Stroke == DiagramStroke.Rule ? "pmd-d-line pmd-d-rule" : "pmd-d-line");
                _builder.AddAttribute(12, "points", Points(line.Points));
                _builder.AddAttribute(13, "stroke-width", Number(line.Thickness));
                if (line.Dash > 0)
                {
                    _builder.AddAttribute(14, "stroke-dasharray", Number(line.Dash));
                }
                _builder.CloseElement();
                break;
            case DiagramText text:
                _builder.OpenElement(15, "text");
                _builder.AddAttribute(16, "x", Number(text.X));
                _builder.AddAttribute(17, "y", Number(text.Y));
                _builder.AddContent(18, text.Text);
                _builder.CloseElement();
                break;
            default:
                // NOTE: a shape type is recorded only when a case above drew it, so the shape coverage test fails for a
                // type added to the drawing that this viewer does not draw.
                return;
        }
        _renderedShapes.Add(shape.GetType());
    }

    private static string Points(IReadOnlyList<DiagramPoint> points) =>
        string.Join(' ', points.Select(point => Number(point.X) + "," + Number(point.Y)));

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}
