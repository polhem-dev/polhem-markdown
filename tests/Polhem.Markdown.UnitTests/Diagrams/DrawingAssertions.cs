using Polhem.Markdown.Diagrams;

namespace Polhem.Markdown.UnitTests.Diagrams;

internal static class DrawingAssertions
{
    public static DiagramDrawing Draw(string code)
    {
        Assert.True(MermaidDiagram.TryDraw(code, out var drawing), "The diagram did not parse:\n" + code);
        AssertInside(drawing);
        return drawing;
    }

    public static IReadOnlyList<DiagramText> Texts(DiagramDrawing drawing) => drawing.Shapes.OfType<DiagramText>().ToList();

    public static DiagramText Text(DiagramDrawing drawing, string text) => Assert.Single(Texts(drawing), shape => shape.Text == text);

    /// <summary>The box drawn around the node or participant whose label is <paramref name="text"/>.</summary>
    public static DiagramRectangle BoxAround(DiagramDrawing drawing, string text)
    {
        var label = Text(drawing, text);
        return Assert.Single(drawing.Shapes.OfType<DiagramRectangle>(), box =>
            box.X < label.X && label.X < box.X + box.Width && box.Y < label.Y && label.Y < box.Y + box.Height);
    }

    /// <summary>Every shape, and the estimated extent of every text, lies inside the drawing.</summary>
    private static void AssertInside(DiagramDrawing drawing)
    {
        foreach (var shape in drawing.Shapes)
        {
            var (left, top, right, bottom) = shape switch
            {
                DiagramRectangle box => (box.X, box.Y, box.X + box.Width, box.Y + box.Height),
                DiagramPolygon polygon => (polygon.Points.Min(p => p.X), polygon.Points.Min(p => p.Y), polygon.Points.Max(p => p.X), polygon.Points.Max(p => p.Y)),
                DiagramLine line => (line.Points.Min(p => p.X), line.Points.Min(p => p.Y), line.Points.Max(p => p.X), line.Points.Max(p => p.Y)),
                DiagramText text => (text.X - TextMetrics.Width(text.Text) / 2, text.Y - TextMetrics.LineHeight / 2,
                    text.X + TextMetrics.Width(text.Text) / 2, text.Y + TextMetrics.LineHeight / 2),
                _ => throw new ArgumentOutOfRangeException(nameof(drawing), shape.GetType().Name),
            };
            Assert.True(left >= 0 && top >= 0 && right <= drawing.Width + 0.01 && bottom <= drawing.Height + 0.01, $"{shape} lies outside {drawing.Width}x{drawing.Height}");
        }
    }
}
