using Polhem.Markdown.Diagrams;
using static Polhem.Markdown.UnitTests.Diagrams.DrawingAssertions;

namespace Polhem.Markdown.UnitTests.Diagrams;

public class SequenceTests
{
    [Fact(DisplayName = "Participants stand in a row in the order they are declared or first used, with boxes at top and bottom")]
    public void Layout_Participants_InOrder()
    {
        var drawing = Draw("sequenceDiagram\n  participant B as Server\n  A->>B: request\n  B-->>A: response");

        var server = Texts(drawing).Where(text => text.Text == "Server").OrderBy(text => text.Y).ToList();
        var client = Texts(drawing).Where(text => text.Text == "A").OrderBy(text => text.Y).ToList();
        Assert.Equal(2, server.Count);
        Assert.Equal(2, client.Count);
        Assert.True(server[0].X < client[0].X, "Declared participants come first.");
        Assert.Equal(4, drawing.Shapes.OfType<DiagramRectangle>().Count());
    }

    [Fact(DisplayName = "Lifelines are dashed in the rule color")]
    public void Layout_Lifelines_AreRuleColored()
    {
        var drawing = Draw("sequenceDiagram\n  A->>B: hi");

        var lifelines = drawing.Shapes.OfType<DiagramLine>().Where(line => line.Stroke == DiagramStroke.Rule).ToList();
        Assert.Equal(2, lifelines.Count);
        Assert.All(lifelines, line => Assert.True(line.Dash > 0));
    }

    [Theory(DisplayName = "Each arrow draws a solid or dotted message, with an arrowhead for the double-headed forms")]
    [InlineData("->>", 0, true)]
    [InlineData("-->>", 3, true)]
    [InlineData("->", 0, false)]
    [InlineData("-->", 3, false)]
    public void Layout_Arrows(string arrow, double dash, bool head)
    {
        var drawing = Draw("sequenceDiagram\n  A" + arrow + "B: hi");

        var message = Assert.Single(drawing.Shapes.OfType<DiagramLine>(), line => line.Stroke == DiagramStroke.Text);
        Assert.Equal(dash, message.Dash);
        Assert.Equal(head ? 1 : 0, drawing.Shapes.OfType<DiagramPolygon>().Count(polygon => polygon.IsArrowHead));
    }

    [Fact(DisplayName = "Messages go down the page in order, each label above its line")]
    public void Layout_Messages_GoDown()
    {
        var drawing = Draw("sequenceDiagram\n  A->>B: first\n  B->>A: second");

        var first = Text(drawing, "first");
        var second = Text(drawing, "second");
        Assert.True(second.Y > first.Y);
        var lines = drawing.Shapes.OfType<DiagramLine>().Where(line => line.Stroke == DiagramStroke.Text).ToList();
        Assert.True(lines[0].Points[0].Y > first.Y && lines[0].Points[0].Y < second.Y);
    }

    [Fact(DisplayName = "A long label pushes the participants apart so it fits between them")]
    public void Layout_LongLabel_WidensColumns()
    {
        const string Label = "a rather long message label that needs room";
        var drawing = Draw("sequenceDiagram\n  A->>B: " + Label);

        var a = Texts(drawing).First(text => text.Text == "A");
        var b = Texts(drawing).First(text => text.Text == "B");
        Assert.True(b.X - a.X >= TextMetrics.Width(Label));
    }

    [Fact(DisplayName = "A message to oneself is a loop to the right with its label beside it")]
    public void Layout_SelfMessage_IsLoop()
    {
        var drawing = Draw("sequenceDiagram\n  A->>A: think");

        var loop = Assert.Single(drawing.Shapes.OfType<DiagramLine>(), line => line.Stroke == DiagramStroke.Text);
        Assert.Equal(4, loop.Points.Count);
        Assert.True(Text(drawing, "think").X > loop.Points[1].X);
    }

    [Theory(DisplayName = "A note stands beside or over its participants")]
    [InlineData("Note right of A: n", 1)]
    [InlineData("Note left of B: n", -1)]
    [InlineData("note over A: n", 0)]
    [InlineData("Note over A,B: n", 0)]
    public void Layout_Notes(string note, int side)
    {
        var drawing = Draw("sequenceDiagram\n  participant A\n  participant B\n  " + note);

        var text = Text(drawing, "n");
        var anchor = Texts(drawing).First(t => t.Text == (note.Contains("left", StringComparison.Ordinal) ? "B" : "A"));
        if (note.Contains("A,B", StringComparison.Ordinal))
        {
            var b = Texts(drawing).First(t => t.Text == "B");
            Assert.Equal((anchor.X + b.X) / 2, text.X, 1);
            Assert.True(BoxAround(drawing, "n").Width > b.X - anchor.X);
        }
        else
        {
            Assert.Equal(side, Math.Sign(Math.Round(text.X - anchor.X)));
        }
    }

    [Fact(DisplayName = "A trailing semicolon is accepted")]
    public void Parse_TrailingSemicolon()
    {
        Text(Draw("sequenceDiagram\n  A->>B: hi;"), "hi");
    }
}
