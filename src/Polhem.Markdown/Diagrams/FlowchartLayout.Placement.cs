namespace Polhem.Markdown.Diagrams;

// The order of the vertices within each rank, and their coordinates.
internal sealed partial class FlowchartLayout
{
    private void Order()
    {
        SetOrder(_ranks);
        var best = _ranks.Select(rank => rank.ToList()).ToList();
        var bestCrossings = Crossings();

        for (var sweep = 0; sweep < OrderingSweeps && bestCrossings > 0; sweep++)
        {
            Reorder(down: sweep % 2 == 0);

            var crossings = Crossings();
            if (crossings < bestCrossings)
            {
                bestCrossings = crossings;
                best = _ranks.Select(rank => rank.ToList()).ToList();
            }
        }

        _ranks = best;
        SetOrder(_ranks);
    }

    /// <summary>
    /// Sorts each rank by the mean position of its neighbours in the rank before it, going down from the top or up from
    /// the bottom. A vertex without such neighbours keeps its position.
    /// </summary>
    private void Reorder(bool down)
    {
        var neighbours = down ? _predecessors : _successors;
        for (var step = 1; step < _ranks.Count; step++)
        {
            var rank = down ? step : _ranks.Count - 1 - step;
            _ranks[rank] = _ranks[rank]
                .OrderBy(vertex => neighbours[vertex].Count == 0
                    ? _vertices[vertex].Order
                    : neighbours[vertex].Average(other => _vertices[other].Order))
                .ToList();
            SetOrder([_ranks[rank]]);
        }
    }

    private void SetOrder(IEnumerable<List<int>> ranks)
    {
        foreach (var rank in ranks)
        {
            for (var index = 0; index < rank.Count; index++)
            {
                _vertices[rank[index]].Order = index;
            }
        }
    }

    private int Crossings()
    {
        var total = 0;
        foreach (var rank in _ranks)
        {
            var segments = rank.SelectMany(from => _successors[from].Select(to => (From: _vertices[from].Order, To: _vertices[to].Order))).ToList();
            for (var i = 0; i < segments.Count; i++)
            {
                for (var j = i + 1; j < segments.Count; j++)
                {
                    if ((segments[i].From - segments[j].From) * (segments[i].To - segments[j].To) < 0)
                    {
                        total++;
                    }
                }
            }
        }
        return total;
    }

    // ───── Coordinates ─────

    private void Place()
    {
        Pack();
        for (var sweep = 0; sweep < PlacementSweeps; sweep++)
        {
            AlignSweep(sweep % 3);
        }
        PlaceAlong();
    }

    /// <summary>Puts the vertices of each rank side by side at their minimum separation.</summary>
    private void Pack()
    {
        foreach (var rank in _ranks)
        {
            var u = 0.0;
            for (var index = 0; index < rank.Count; index++)
            {
                if (index > 0)
                {
                    u += Separation(rank[index - 1], rank[index]);
                }
                _vertices[rank[index]].U = u;
            }
        }
    }

    /// <summary>
    /// Aligns every rank with its neighbours: those above it going down (mode 0), those below it going up (mode 1), or
    /// both going down (mode 2).
    /// </summary>
    private void AlignSweep(int mode)
    {
        var order = mode == 1 ? Enumerable.Range(0, _ranks.Count).Reverse() : Enumerable.Range(0, _ranks.Count);
        foreach (var rank in order)
        {
            Align(_ranks[rank], vertex => mode switch
            {
                0 => _predecessors[vertex],
                1 => _successors[vertex],
                _ => _predecessors[vertex].Concat(_successors[vertex]),
            });
        }
    }

    /// <summary>Places the ranks one after another along the chart's direction, and maps both axes to x and y.</summary>
    private void PlaceAlong()
    {
        var along = 0.0;
        for (var rank = 0; rank < _ranks.Count; rank++)
        {
            var thickness = _ranks[rank].Select(vertex => _vertices[vertex].Along).DefaultIfEmpty(0).Max();
            foreach (var vertex in _ranks[rank])
            {
                _vertices[vertex].V = along + thickness / 2;
            }
            along += thickness + RankGap(rank);
        }

        foreach (var vertex in _vertices)
        {
            (vertex.X, vertex.Y) = _chart.Direction switch
            {
                FlowDirection.BottomToTop => (vertex.U, -vertex.V),
                FlowDirection.LeftToRight => (vertex.V, vertex.U),
                FlowDirection.RightToLeft => (-vertex.V, vertex.U),
                _ => (vertex.U, vertex.V),
            };
        }
    }

    private double Separation(int left, int right)
    {
        var gap = _vertices[left].IsPlaceholder || _vertices[right].IsPlaceholder ? PlaceholderGap : NodeGap;
        return (_vertices[left].Across + _vertices[right].Across) / 2 + gap;
    }

    /// <summary>
    /// Moves the vertices of a rank as close as possible to the mean position of their neighbours while keeping their
    /// order and separation: a least-squares fit under ordering constraints, solved by pooling adjacent violators.
    /// </summary>
    private void Align(List<int> rank, Func<int, IEnumerable<int>> neighbours)
    {
        var count = rank.Count;
        var offsets = new double[count];
        var targets = new double[count];
        for (var index = 0; index < count; index++)
        {
            offsets[index] = index == 0 ? 0 : offsets[index - 1] + Separation(rank[index - 1], rank[index]);
            var vertex = rank[index];
            var others = neighbours(vertex).ToList();
            var desired = others.Count == 0 ? _vertices[vertex].U : others.Average(other => _vertices[other].U);
            targets[index] = desired - offsets[index];
        }

        var blocks = new List<(double Sum, int Count)>();
        foreach (var target in targets)
        {
            blocks.Add((target, 1));
            while (blocks.Count > 1 && blocks[^2].Sum / blocks[^2].Count > blocks[^1].Sum / blocks[^1].Count)
            {
                var merged = (blocks[^2].Sum + blocks[^1].Sum, blocks[^2].Count + blocks[^1].Count);
                blocks.RemoveAt(blocks.Count - 1);
                blocks[^1] = merged;
            }
        }

        var position = 0;
        foreach (var (sum, blockCount) in blocks)
        {
            for (var index = 0; index < blockCount; index++, position++)
            {
                _vertices[rank[position]].U = sum / blockCount + offsets[position];
            }
        }
    }

    /// <summary>The gap after a rank, widened when a link label sits in it.</summary>
    private double RankGap(int rank)
    {
        var gap = _horizontal ? HorizontalRankGap : VerticalRankGap;
        for (var index = 0; index < _paths.Count; index++)
        {
            var label = _chart.Edges[index].Label;
            var segment = _labelSegments[index];
            if (segment < 0 || !HasText(label))
            {
                continue;
            }
            var path = _paths[index];
            if (Math.Min(_vertices[path[segment]].Rank, _vertices[path[segment + 1]].Rank) != rank)
            {
                continue;
            }
            var extent = _horizontal ? TextMetrics.Width(label) + 16 : TextMetrics.Height(label) + 8;
            gap = Math.Max(gap, extent + 2 * LabelGap);
        }
        return gap;
    }
}
