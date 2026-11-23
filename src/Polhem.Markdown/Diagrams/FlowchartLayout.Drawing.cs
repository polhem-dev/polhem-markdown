namespace Polhem.Markdown.Diagrams;

// Turns the placed vertices into shapes: nodes, links with their labels, and loops.
internal sealed partial class FlowchartLayout
{
    private static readonly double[] s_labelPositions = [0.5, 0.3, 0.7, 0.15, 0.85];

    private DiagramDrawing Draw()
    {
        var builder = new DrawingBuilder();
        var labels = new List<LabelRequest>();
        var bends = ParallelBends();

        for (var index = 0; index < _chart.Edges.Count; index++)
        {
            var edge = _chart.Edges[index];
            var thickness = edge.Style == FlowLineStyle.Thick ? DrawingBuilder.ThickLineThickness : DrawingBuilder.LineThickness;
            var dash = edge.Style == FlowLineStyle.Dotted ? 3 : 0;
            if (edge.From == edge.To)
            {
                Loop(builder, edge, thickness, dash, labels);
            }
            else
            {
                Link(builder, index, bends[index], thickness, dash, labels);
            }
        }

        for (var index = 0; index < _chart.Nodes.Count; index++)
        {
            Node(builder, _chart.Nodes[index], _vertices[index]);
        }
        PlaceLabels(builder, labels);
        return builder.Build();
    }

    /// <summary>A label to place, with the centers it may take in order of preference.</summary>
    private sealed record LabelRequest(IReadOnlyList<string> Lines, List<DiagramPoint> Candidates);

    /// <summary>Draws a link between two different nodes, bent by <paramref name="bend"/> when it is not zero.</summary>
    private void Link(DrawingBuilder builder, int index, DiagramPoint bend, double thickness, double dash, List<LabelRequest> labels)
    {
        var edge = _chart.Edges[index];
        var points = _paths[index].Select(vertex => Center(_vertices[vertex])).ToList();
        var bent = bend != default;
        if (bent)
        {
            points.Insert(1, new DiagramPoint((points[0].X + points[1].X) / 2 + bend.X, (points[0].Y + points[1].Y) / 2 + bend.Y));
        }
        points[0] = Clip(edge.From, points[1]);
        points[^1] = Clip(edge.To, points[^2]);
        builder.Connector(points, thickness, dash, edge.Arrow);

        if (!HasText(edge.Label))
        {
            return;
        }
        if (bent)
        {
            // The outer side of the bend first: the side it leans to.
            var side = BendSide(bend);
            var candidates = new List<DiagramPoint> { Beside(points[0], points[1], 1, edge.Label, side) };
            candidates.AddRange(BesideSegment(points[0], points[1], edge.Label, side));
            candidates.AddRange(BesideSegment(points[1], points[2], edge.Label, side));
            labels.Add(new LabelRequest(edge.Label, candidates));
        }
        else
        {
            var segment = _labelSegments[index];
            labels.Add(new LabelRequest(edge.Label, BesideSegment(points[segment], points[segment + 1], edge.Label, 1).ToList()));
        }
    }

    /// <summary>The side a bend leans to, as <see cref="Beside"/> numbers sides: 1 above or to the right, -1 otherwise.</summary>
    private int BendSide(DiagramPoint bend)
    {
        if (_horizontal)
        {
            return bend.Y < 0 ? 1 : -1;
        }
        return bend.X > 0 ? 1 : -1;
    }

    /// <summary>
    /// The offset of the middle of each link that shares both of its nodes with another link without placeholders
    /// between them, such as a link and its way back. The links of such a group bend apart symmetrically instead of being
    /// drawn over each other; a link alone, or the middle one of an odd group, stays straight.
    /// </summary>
    private DiagramPoint[] ParallelBends()
    {
        var bends = new DiagramPoint[_chart.Edges.Count];
        var groups = Enumerable.Range(0, _chart.Edges.Count)
            .Where(index => _paths[index].Count == 2)
            .GroupBy(index => (Math.Min(_chart.Edges[index].From, _chart.Edges[index].To), Math.Max(_chart.Edges[index].From, _chart.Edges[index].To)))
            .Where(group => group.Count() > 1);
        foreach (var group in groups)
        {
            var (low, high) = group.Key;
            var dx = _vertices[high].X - _vertices[low].X;
            var dy = _vertices[high].Y - _vertices[low].Y;
            var length = Math.Sqrt(dx * dx + dy * dy);
            var (nx, ny) = (-dy / length, dx / length);
            var members = group.ToList();
            for (var position = 0; position < members.Count; position++)
            {
                var offset = (position - (members.Count - 1) / 2.0) * ParallelGap;
                bends[members[position]] = new DiagramPoint(nx * offset, ny * offset);
            }
        }
        return bends;
    }

    private static void Node(DrawingBuilder builder, FlowNode node, Vertex vertex)
    {
        var (x, y, w, h) = (vertex.X - vertex.Width / 2, vertex.Y - vertex.Height / 2, vertex.Width, vertex.Height);
        switch (node.Shape)
        {
            case FlowNodeShape.Rounded:
                builder.Rectangle(x, y, w, h, 6);
                break;
            case FlowNodeShape.Stadium:
                builder.Rectangle(x, y, w, h, h / 2);
                break;
            case FlowNodeShape.Circle:
                builder.Rectangle(x, y, w, h, w / 2);
                break;
            case FlowNodeShape.Diamond:
                builder.Polygon([new(vertex.X, y), new(x + w, vertex.Y), new(vertex.X, y + h), new(x, vertex.Y)]);
                break;
            case FlowNodeShape.Hexagon:
                var inset = h / 4;
                builder.Polygon(
                [
                    new(x + inset, y), new(x + w - inset, y), new(x + w, vertex.Y),
                    new(x + w - inset, y + h), new(x + inset, y + h), new(x, vertex.Y),
                ]);
                break;
            default:
                builder.Rectangle(x, y, w, h, 0);
                break;
        }
        builder.Text(vertex.X, vertex.Y, node.Lines);
    }

    /// <summary>
    /// Candidate centers for a label beside the segment from <paramref name="a"/> to <paramref name="b"/>: first on the
    /// preferred side (to the right in a vertical chart, above in a horizontal one, or the side <paramref name="side"/>
    /// names), at the middle and then further along, and then the same on the other side.
    /// </summary>
    private IEnumerable<DiagramPoint> BesideSegment(DiagramPoint a, DiagramPoint b, IReadOnlyList<string> label, int side)
    {
        foreach (var currentSide in new[] { side, -side })
        {
            foreach (var t in s_labelPositions)
            {
                yield return Beside(a, b, t, label, currentSide);
            }
        }
    }

    /// <summary>
    /// The center of a label at <paramref name="t"/> along a segment, on side 1 (right of a vertical chart's link, above a
    /// horizontal chart's) or -1, moved out further when the segment slants under it.
    /// </summary>
    private DiagramPoint Beside(DiagramPoint a, DiagramPoint b, double t, IReadOnlyList<string> label, int side)
    {
        var at = new DiagramPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
        var dx = Math.Abs(b.X - a.X);
        var dy = Math.Abs(b.Y - a.Y);
        var width = TextMetrics.Width(label);
        var height = TextMetrics.Height(label);
        if (_horizontal)
        {
            var slant = dx < 0.001 ? 0 : Math.Min(dy / dx * width / 2, 60);
            return new DiagramPoint(at.X, at.Y - side * (LabelGap + slant + height / 2));
        }
        var verticalSlant = dy < 0.001 ? 0 : Math.Min(dx / dy * height / 2, 60);
        return new DiagramPoint(at.X + side * (LabelGap + verticalSlant + width / 2), at.Y);
    }

    /// <summary>
    /// Draws each label at its first candidate that overlaps no node and no label placed before it, or at its first
    /// candidate when every one overlaps.
    /// </summary>
    private void PlaceLabels(DrawingBuilder builder, List<LabelRequest> labels)
    {
        var taken = _vertices.Take(_chart.Nodes.Count)
            .Select(vertex => Box(vertex.X, vertex.Y, vertex.Width, vertex.Height))
            .ToList();
        foreach (var (lines, candidates) in labels)
        {
            var width = TextMetrics.Width(lines);
            var height = TextMetrics.Height(lines);
            var chosen = candidates.FirstOrDefault(
                candidate => !taken.Any(box => Overlaps(box, Box(candidate.X, candidate.Y, width, height))),
                candidates[0]);
            taken.Add(Box(chosen.X, chosen.Y, width, height));
            builder.Text(chosen.X, chosen.Y, lines);
        }

        static (double Left, double Top, double Right, double Bottom) Box(double x, double y, double width, double height) =>
            (x - width / 2 - 2, y - height / 2 - 2, x + width / 2 + 2, y + height / 2 + 2);

        static bool Overlaps((double Left, double Top, double Right, double Bottom) a, (double Left, double Top, double Right, double Bottom) b) =>
            a.Left < b.Right && b.Left < a.Right && a.Top < b.Bottom && b.Top < a.Bottom;
    }

    /// <summary>A link from a node to itself: a loop beside the node, to the right in a vertical chart and below in a horizontal one.</summary>
    private void Loop(DrawingBuilder builder, FlowEdge edge, double thickness, double dash, List<LabelRequest> labels)
    {
        var vertex = _vertices[edge.From];
        var center = Center(vertex);
        var (nx, ny) = _horizontal ? (0.0, 1.0) : (1.0, 0.0);
        var (tx, ty) = (ny, nx);
        const double Far = 1000;
        const double Spread = 0.35;

        var start = Clip(edge.From, new(center.X + (nx - tx * Spread) * Far, center.Y + (ny - ty * Spread) * Far));
        var end = Clip(edge.From, new(center.X + (nx + tx * Spread) * Far, center.Y + (ny + ty * Spread) * Far));
        var reach = (_horizontal ? vertex.Height : vertex.Width) / 2 + LoopReach;
        var outerStart = new DiagramPoint(_horizontal ? start.X : center.X + reach, _horizontal ? center.Y + reach : start.Y);
        var outerEnd = new DiagramPoint(_horizontal ? end.X : center.X + reach, _horizontal ? center.Y + reach : end.Y);
        builder.Connector([start, outerStart, outerEnd, end], thickness, dash, edge.Arrow);

        if (HasText(edge.Label))
        {
            var at = _horizontal
                ? new DiagramPoint(center.X, center.Y + reach + LabelGap + TextMetrics.Height(edge.Label) / 2)
                : new DiagramPoint(center.X + reach + LabelGap + TextMetrics.Width(edge.Label) / 2, center.Y);
            labels.Add(new LabelRequest(edge.Label, [at]));
        }
    }

    /// <summary>The point where a line from the center of a node toward <paramref name="toward"/> leaves its outline.</summary>
    private DiagramPoint Clip(int node, DiagramPoint toward)
    {
        var vertex = _vertices[node];
        var dx = toward.X - vertex.X;
        var dy = toward.Y - vertex.Y;
        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001)
        {
            return toward;
        }

        var halfWidth = vertex.Width / 2;
        var halfHeight = vertex.Height / 2;
        var t = _chart.Nodes[node].Shape switch
        {
            FlowNodeShape.Circle => halfWidth / Math.Sqrt(dx * dx + dy * dy),
            FlowNodeShape.Diamond => 1 / (Math.Abs(dx) / halfWidth + Math.Abs(dy) / halfHeight),
            _ => Math.Min(Math.Abs(dx) < 0.001 ? double.MaxValue : halfWidth / Math.Abs(dx),
                Math.Abs(dy) < 0.001 ? double.MaxValue : halfHeight / Math.Abs(dy)),
        };
        return t >= 1 ? toward : new DiagramPoint(vertex.X + dx * t, vertex.Y + dy * t);
    }

    private static DiagramPoint Center(Vertex vertex) => new(vertex.X, vertex.Y);

    private static bool HasText(IReadOnlyList<string> lines) => lines.Any(line => line.Length > 0);
}
