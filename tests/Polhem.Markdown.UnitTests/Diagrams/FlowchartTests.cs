using Polhem.Markdown.Diagrams;
using static Polhem.Markdown.UnitTests.Diagrams.DrawingAssertions;

namespace Polhem.Markdown.UnitTests.Diagrams;

public class FlowchartTests
{
    private static readonly string[] s_siblings = ["B", "C", "D"];

    [Theory(DisplayName = "The direction decides where the second node goes")]
    [InlineData("flowchart TD", 0, 1)]
    [InlineData("flowchart TB", 0, 1)]
    [InlineData("graph", 0, 1)]
    [InlineData("flowchart BT", 0, -1)]
    [InlineData("flowchart LR", 1, 0)]
    [InlineData("graph RL", -1, 0)]
    public void Layout_Direction_PlacesSecondNode(string header, int dx, int dy)
    {
        var drawing = Draw(header + "\n  A --> B");

        var a = Text(drawing, "A");
        var b = Text(drawing, "B");
        Assert.Equal(dx, Math.Sign(Math.Round(b.X - a.X)));
        Assert.Equal(dy, Math.Sign(Math.Round(b.Y - a.Y)));
    }

    [Fact(DisplayName = "Statements may share a line, separated by semicolons, also after the header")]
    public void Parse_Semicolons_SeparateStatements()
    {
        var drawing = Draw("graph TD; A-->B; B-->C;");

        Assert.Equal(["A", "B", "C"], Texts(drawing).Select(text => text.Text).Order());
    }

    [Fact(DisplayName = "Each node shape is drawn with its own outline")]
    public void Layout_Shapes_HaveTheirOutlines()
    {
        var drawing = Draw("flowchart LR\n  a[Box] --- b(Round) --- c([Pill]) --- d((Ring)) --- e{Choice} --- f{{Hex}}");

        Assert.Equal(0, BoxAround(drawing, "Box").CornerRadius);
        Assert.InRange(BoxAround(drawing, "Round").CornerRadius, 1, 10);
        var pill = BoxAround(drawing, "Pill");
        Assert.Equal(pill.Height / 2, pill.CornerRadius, 1);
        var ring = BoxAround(drawing, "Ring");
        Assert.Equal(ring.Width, ring.Height, 1);
        Assert.Equal(ring.Width / 2, ring.CornerRadius, 1);
        var outlines = drawing.Shapes.OfType<DiagramPolygon>().Where(polygon => !polygon.IsArrowHead).ToList();
        Assert.Equal([4, 6], outlines.Select(polygon => polygon.Points.Count).Order());
    }

    [Fact(DisplayName = "A node keeps the label of its definition wherever it is referred to later")]
    public void Parse_Label_FromDefinition()
    {
        var drawing = Draw("flowchart TD\n  A[Start here] --> B\n  B --> A");

        Assert.Equal(["B", "Start here"], Texts(drawing).Select(text => text.Text).Order());
    }

    [Theory(DisplayName = "Links draw solid, dotted or thick lines, with an arrowhead only when the link has one")]
    [InlineData("A --> B", 0, DrawingBuilder.LineThickness, true)]
    [InlineData("A --- B", 0, DrawingBuilder.LineThickness, false)]
    [InlineData("A ----> B", 0, DrawingBuilder.LineThickness, true)]
    [InlineData("A -.-> B", 3, DrawingBuilder.LineThickness, true)]
    [InlineData("A -.- B", 3, DrawingBuilder.LineThickness, false)]
    [InlineData("A ==> B", 0, DrawingBuilder.ThickLineThickness, true)]
    [InlineData("A === B", 0, DrawingBuilder.ThickLineThickness, false)]
    public void Layout_LinkStyles(string link, double dash, double thickness, bool arrow)
    {
        var drawing = Draw("flowchart TD\n  " + link);

        var line = Assert.Single(drawing.Shapes.OfType<DiagramLine>());
        Assert.Equal(dash, line.Dash);
        Assert.Equal(thickness, line.Thickness);
        Assert.Equal(DiagramStroke.Text, line.Stroke);
        Assert.Equal(arrow ? 1 : 0, drawing.Shapes.OfType<DiagramPolygon>().Count(polygon => polygon.IsArrowHead));
    }

    [Theory(DisplayName = "A link label is drawn in every way Mermaid writes one")]
    [InlineData("A -->|yes| B")]
    [InlineData("A -->|\"yes\"| B")]
    [InlineData("A -- yes --> B")]
    [InlineData("A -. yes .-> B")]
    [InlineData("A == yes ==> B")]
    [InlineData("A ---|yes| B")]
    public void Layout_LinkLabel_IsDrawn(string link)
    {
        var drawing = Draw("flowchart TD\n  " + link);

        Text(drawing, "yes");
        Assert.Single(drawing.Shapes.OfType<DiagramLine>());
    }

    [Fact(DisplayName = "A link label in a vertical chart sits beside the line, between the two nodes")]
    public void Layout_LinkLabel_BetweenNodes()
    {
        var drawing = Draw("flowchart TD\n  A -->|label| B");

        var label = Text(drawing, "label");
        var a = BoxAround(drawing, "A");
        var b = BoxAround(drawing, "B");
        Assert.InRange(label.Y, a.Y + a.Height, b.Y);
        Assert.True(label.X - TextMetrics.Width("label") / 2 > a.X + a.Width / 2, "The label overlaps the line.");
    }

    [Fact(DisplayName = "A line break in a label gives the text two lines and the node room for both")]
    public void Layout_LineBreak_TwoLines()
    {
        var single = Draw("flowchart TD\n  A[one]");
        var doubled = Draw("flowchart TD\n  A[\"one<br/>two\"]");

        Assert.Equal(2, Texts(doubled).Count);
        Assert.True(BoxAround(doubled, "one").Height >= BoxAround(single, "one").Height + TextMetrics.LineHeight - 0.01);
    }

    [Fact(DisplayName = "Chinese text is estimated wider than the same number of Latin letters")]
    public void Layout_WideText_IsWider()
    {
        Assert.True(TextMetrics.Width("開始處理") > TextMetrics.Width("abcd"));
        Assert.True(BoxAround(Draw("flowchart TD\n  A[開始處理]"), "開始處理").Width > BoxAround(Draw("flowchart TD\n  A[abcd]"), "abcd").Width);
    }

    [Fact(DisplayName = "A cycle is laid out, with every link drawn")]
    public void Layout_Cycle_DrawsEveryLink()
    {
        var drawing = Draw("flowchart TD\n  A --> B --> C --> A\n  C --> D");

        Assert.Equal(4, drawing.Shapes.OfType<DiagramLine>().Count());
        Assert.Equal(4, drawing.Shapes.OfType<DiagramPolygon>().Count(polygon => polygon.IsArrowHead));
    }

    [Theory(DisplayName = "A link from a node to itself is drawn as a loop beside it")]
    [InlineData("TD")]
    [InlineData("LR")]
    public void Layout_SelfLink_IsLoop(string direction)
    {
        var drawing = Draw("flowchart " + direction + "\n  A -->|again| A");

        var loop = Assert.Single(drawing.Shapes.OfType<DiagramLine>());
        Assert.Equal(4, loop.Points.Count);
        Text(drawing, "again");
    }

    [Fact(DisplayName = "A link and its way back bend apart, each label on its own side")]
    public void Layout_LinkAndWayBack_BendApart()
    {
        var drawing = Draw("flowchart LR\n  A -->|there| B\n  B -->|back| A");

        var lines = drawing.Shapes.OfType<DiagramLine>().ToList();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Equal(3, line.Points.Count));
        Assert.Equal(-1, Math.Sign(lines[0].Points[1].Y - Text(drawing, "A").Y) * Math.Sign(lines[1].Points[1].Y - Text(drawing, "A").Y));
        var there = Text(drawing, "there");
        var back = Text(drawing, "back");
        Assert.True(Math.Abs(there.Y - back.Y) >= TextMetrics.LineHeight, "The labels overlap.");
    }

    [Fact(DisplayName = "Nodes in the same rank do not overlap")]
    public void Layout_SiblingNodes_DoNotOverlap()
    {
        var drawing = Draw("flowchart TD\n  A --> B\n  A --> C\n  A --> D\n  B --> E\n  D --> E");

        var boxes = s_siblings.Select(label => BoxAround(drawing, label)).OrderBy(box => box.X).ToList();
        for (var index = 1; index < boxes.Count; index++)
        {
            Assert.True(boxes[index].X >= boxes[index - 1].X + boxes[index - 1].Width, "Neighbouring nodes overlap.");
        }
    }

    [Fact(DisplayName = "A link across several ranks still ends at its target node")]
    public void Layout_LongLink_EndsAtTarget()
    {
        var drawing = Draw("flowchart TD\n  A --> B --> C --> D\n  A --> D");

        var d = BoxAround(drawing, "D");
        var tips = drawing.Shapes.OfType<DiagramPolygon>().Where(polygon => polygon.IsArrowHead).Select(polygon => polygon.Points[0]);
        Assert.Equal(2, tips.Count(tip => Math.Abs(tip.Y - d.Y) < 0.5 && tip.X >= d.X - 0.5 && tip.X <= d.X + d.Width + 0.5));
    }

    [Fact(DisplayName = "The same text is always laid out the same way")]
    public void Layout_IsDeterministic()
    {
        const string Chart = "flowchart LR\n  A --> B\n  A --> C\n  B --> D\n  C --> D\n  D -.->|back| A";

        Assert.Equal(Describe(Draw(Chart)), Describe(Draw(Chart)));

        static string Describe(DiagramDrawing drawing) => string.Join("\n", drawing.Shapes.Select(shape => shape switch
        {
            DiagramPolygon polygon => string.Join(" ", polygon.Points),
            DiagramLine line => string.Join(" ", line.Points),
            _ => shape.ToString(),
        }));
    }
}
