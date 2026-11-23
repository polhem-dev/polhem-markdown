namespace Polhem.Markdown.Diagrams;

/// <summary>
/// Lays out a flowchart in ranks: cycles are broken, nodes are put in ranks by their longest path from a source, links
/// that span several ranks pass through placeholder vertices, the order within each rank is chosen to reduce crossings,
/// and the vertices are then placed close to their neighbours.
/// </summary>
/// <remarks>
/// The layout works in two axes, along the ranks and across them, and maps them to x and y by the chart's direction at
/// the end, so one implementation serves all four directions.
/// </remarks>
internal sealed partial class FlowchartLayout
{
    /// <summary>The most vertices, nodes and placeholders together, before a chart shows as code.</summary>
    private const int MaxVertices = 1500;

    private const double PaddingX = 16;
    private const double PaddingY = 10;
    private const double NodeGap = 40;
    private const double PlaceholderGap = 14;
    private const double VerticalRankGap = 44;
    private const double HorizontalRankGap = 56;
    private const double LabelGap = 6;
    private const double LoopReach = 24;
    private const double ParallelGap = 26;
    private const int OrderingSweeps = 12;
    private const int PlacementSweeps = 9;

    private readonly Flowchart _chart;
    private readonly bool _horizontal;
    private readonly List<Vertex> _vertices = [];
    private readonly List<List<int>> _paths = [];
    private readonly List<int> _labelSegments = [];
    private readonly List<List<int>> _predecessors = [];
    private readonly List<List<int>> _successors = [];
    private List<List<int>> _ranks = [];

    private FlowchartLayout(Flowchart chart)
    {
        _chart = chart;
        _horizontal = chart.Direction is FlowDirection.LeftToRight or FlowDirection.RightToLeft;
    }

    public static bool TryLayout(Flowchart chart, out DiagramDrawing drawing)
    {
        var layout = new FlowchartLayout(chart);
        drawing = null!;
        if (!layout.TryBuildGraph())
        {
            return false;
        }
        layout.Order();
        layout.Place();
        drawing = layout.Draw();
        return true;
    }

    private sealed class Vertex
    {
        public double Width { get; init; }

        public double Height { get; init; }

        public double Across { get; set; }

        public double Along { get; init; }

        public bool IsPlaceholder { get; init; }

        public int Rank { get; set; }

        public int Order { get; set; }

        /// <summary>The center across the ranks.</summary>
        public double U { get; set; }

        /// <summary>The center along the ranks.</summary>
        public double V { get; set; }

        public double X { get; set; }

        public double Y { get; set; }
    }

    private bool TryBuildGraph()
    {
        foreach (var node in _chart.Nodes)
        {
            var (width, height) = NodeSize(node);
            _vertices.Add(new Vertex
            {
                Width = width,
                Height = height,
                Across = _horizontal ? height : width,
                Along = _horizontal ? width : height,
            });
        }
        ReserveLoopSpace();

        var reversed = ReversedEdges();
        AssignRanks(reversed);
        for (var index = 0; index < _chart.Edges.Count; index++)
        {
            if (!TryAddPath(index, reversed[index]))
            {
                return false;
            }
        }
        ConnectSegments();

        var rankCount = _vertices.Max(vertex => vertex.Rank) + 1;
        _ranks = Enumerable.Range(0, rankCount).Select(_ => new List<int>()).ToList();
        for (var index = 0; index < _vertices.Count; index++)
        {
            _ranks[_vertices[index].Rank].Add(index);
        }
        return true;
    }

    /// <summary>
    /// Adds the path of a link through a placeholder vertex in every rank it crosses, or fails when that would pass
    /// <see cref="MaxVertices"/>. A link to its own node gets an empty path; the loop is drawn beside the node.
    /// </summary>
    private bool TryAddPath(int index, bool reversed)
    {
        var edge = _chart.Edges[index];
        if (edge.From == edge.To)
        {
            _paths.Add([]);
            _labelSegments.Add(-1);
            return true;
        }

        var (from, to) = reversed ? (edge.To, edge.From) : (edge.From, edge.To);
        var chain = new List<int> { from };
        for (var rank = _vertices[from].Rank + 1; rank < _vertices[to].Rank; rank++)
        {
            if (_vertices.Count == MaxVertices)
            {
                return false;
            }
            chain.Add(_vertices.Count);
            _vertices.Add(new Vertex { IsPlaceholder = true, Rank = rank });
        }
        chain.Add(to);

        // The label sits on the first segment in rank order, so it is always in the gap after the upper rank.
        var labelSegment = reversed ? chain.Count - 2 : 0;
        if (reversed)
        {
            chain.Reverse();
        }
        _paths.Add(chain);
        _labelSegments.Add(labelSegment);
        return true;
    }

    /// <summary>Records each segment of every path as a link from the vertex in the upper rank to the one below it.</summary>
    private void ConnectSegments()
    {
        foreach (var _ in _vertices)
        {
            _predecessors.Add([]);
            _successors.Add([]);
        }
        foreach (var path in _paths)
        {
            for (var index = 0; index + 1 < path.Count; index++)
            {
                var (a, b) = _vertices[path[index]].Rank < _vertices[path[index + 1]].Rank
                    ? (path[index], path[index + 1])
                    : (path[index + 1], path[index]);
                _successors[a].Add(b);
                _predecessors[b].Add(a);
            }
        }
    }

    private static (double Width, double Height) NodeSize(FlowNode node)
    {
        var textWidth = TextMetrics.Width(node.Lines);
        var textHeight = TextMetrics.Height(node.Lines);
        var height = textHeight + 2 * PaddingY;
        return node.Shape switch
        {
            FlowNodeShape.Stadium => (textWidth + height + 12, height),
            FlowNodeShape.Circle => Square(Math.Sqrt(textWidth * textWidth + textHeight * textHeight) + 20),
            FlowNodeShape.Diamond => Square(textWidth + textHeight + 36),
            FlowNodeShape.Hexagon => (textWidth + 2 * PaddingX + height / 2, height),
            _ => (Math.Max(textWidth + 2 * PaddingX, 40), height),
        };

        static (double, double) Square(double side) => (side, side);
    }

    /// <summary>Widens a node with a link to itself across the ranks, so the loop and its label do not hit a neighbour.</summary>
    private void ReserveLoopSpace()
    {
        foreach (var edge in _chart.Edges.Where(edge => edge.From == edge.To))
        {
            var label = 0.0;
            if (HasText(edge.Label))
            {
                label = LabelGap + (_horizontal ? TextMetrics.Height(edge.Label) : TextMetrics.Width(edge.Label));
            }
            var vertex = _vertices[edge.From];
            vertex.Across = Math.Max(vertex.Across, (_horizontal ? vertex.Height : vertex.Width) + 2 * (LoopReach + label));
        }
    }

    /// <summary>Finds links that close a cycle, by a depth-first search in source order; they are laid out reversed.</summary>
    private bool[] ReversedEdges()
    {
        var reversed = new bool[_chart.Edges.Count];
        var outgoing = Enumerable.Range(0, _chart.Nodes.Count).Select(_ => new List<int>()).ToList();
        for (var index = 0; index < _chart.Edges.Count; index++)
        {
            outgoing[_chart.Edges[index].From].Add(index);
        }

        var visited = new bool[_chart.Nodes.Count];
        var onPath = new HashSet<int>();
        for (var node = 0; node < _chart.Nodes.Count; node++)
        {
            if (!visited[node])
            {
                Visit(node);
            }
        }
        return reversed;

        void Visit(int node)
        {
            visited[node] = true;
            onPath.Add(node);
            foreach (var index in outgoing[node])
            {
                var target = _chart.Edges[index].To;
                if (onPath.Contains(target))
                {
                    reversed[index] = true;
                }
                else if (!visited[target])
                {
                    Visit(target);
                }
            }
            onPath.Remove(node);
        }
    }

    /// <summary>Puts each node one rank below its lowest predecessor, then pulls sources down next to their successors.</summary>
    private void AssignRanks(bool[] reversed)
    {
        var count = _chart.Nodes.Count;
        var successors = Enumerable.Range(0, count).Select(_ => new List<int>()).ToList();
        var incoming = new int[count];
        for (var index = 0; index < _chart.Edges.Count; index++)
        {
            var edge = _chart.Edges[index];
            if (edge.From == edge.To)
            {
                continue;
            }
            var (from, to) = reversed[index] ? (edge.To, edge.From) : (edge.From, edge.To);
            successors[from].Add(to);
            incoming[to]++;
        }

        var rank = new int[count];
        var remaining = (int[])incoming.Clone();
        var queue = new Queue<int>(Enumerable.Range(0, count).Where(node => remaining[node] == 0));
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            foreach (var next in successors[node])
            {
                rank[next] = Math.Max(rank[next], rank[node] + 1);
                if (--remaining[next] == 0)
                {
                    queue.Enqueue(next);
                }
            }
        }

        for (var node = 0; node < count; node++)
        {
            if (incoming[node] == 0 && successors[node].Count > 0)
            {
                rank[node] = successors[node].Min(next => rank[next]) - 1;
            }
            _vertices[node].Rank = rank[node];
        }
    }
}
