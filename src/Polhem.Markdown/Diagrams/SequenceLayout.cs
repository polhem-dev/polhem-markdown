namespace Polhem.Markdown.Diagrams;

/// <summary>
/// Lays out a sequence diagram: participants in a row, each with a dashed lifeline and its box repeated at the bottom,
/// and the messages and notes stacked down the page in order. Columns are moved apart until every label fits.
/// </summary>
internal static class SequenceLayout
{
    private const double PaddingX = 16;
    private const double PaddingY = 10;
    private const double MinBoxWidth = 80;
    private const double ColumnGap = 40;
    private const double StepGap = 16;
    private const double LabelGap = 6;
    private const double LoopReach = 30;
    private const double LoopHeight = 22;
    private const double NoteGap = 12;
    private const double NotePadding = 10;
    private const double SpanOverhang = 30;
    private const double LifelineDash = 4;

    public static DiagramDrawing Layout(SequenceDiagram diagram)
    {
        var participants = diagram.Participants;
        var boxWidths = participants.Select(p => Math.Max(TextMetrics.Width(p.Lines) + 2 * PaddingX, MinBoxWidth)).ToList();
        var boxHeight = participants.Max(p => TextMetrics.Height(p.Lines)) + 2 * PaddingY;
        var centers = Columns(diagram, boxWidths);

        var builder = new DrawingBuilder();
        var steps = new List<Action>();
        var y = boxHeight + StepGap;
        foreach (var step in diagram.Steps)
        {
            var top = y;
            switch (step)
            {
                case SequenceMessage message when message.From == message.To:
                    y = SelfMessage(steps, builder, message, centers[message.From], top);
                    break;
                case SequenceMessage message:
                    y = Message(steps, builder, message, centers[message.From], centers[message.To], top);
                    break;
                case SequenceNote note:
                    y = Note(steps, builder, note, centers, top);
                    break;
            }
        }
        var bottom = y + 4;

        for (var index = 0; index < participants.Count; index++)
        {
            builder.Line([new(centers[index], boxHeight), new(centers[index], bottom)], DiagramStroke.Rule, 1, LifelineDash);
        }
        for (var index = 0; index < participants.Count; index++)
        {
            foreach (var boxTop in new[] { 0, bottom })
            {
                builder.Rectangle(centers[index] - boxWidths[index] / 2, boxTop, boxWidths[index], boxHeight, 3);
                builder.Text(centers[index], boxTop + boxHeight / 2, participants[index].Lines);
            }
        }
        foreach (var draw in steps)
        {
            draw();
        }
        return builder.Build();
    }

    /// <summary>The centers of the participant columns, spaced so that every message label and note fits.</summary>
    private static double[] Columns(SequenceDiagram diagram, List<double> boxWidths)
    {
        var count = boxWidths.Count;
        var gaps = new double[Math.Max(count - 1, 0)];
        for (var index = 0; index < gaps.Length; index++)
        {
            gaps[index] = (boxWidths[index] + boxWidths[index + 1]) / 2 + ColumnGap;
        }

        var needs = new List<(int Low, int High, double Distance)>();
        foreach (var step in diagram.Steps)
        {
            switch (step)
            {
                case SequenceMessage message when message.From == message.To:
                    needs.Add((message.From, message.From + 1, LoopReach + LabelGap + TextMetrics.Width(message.Text) + NoteGap));
                    break;
                case SequenceMessage message:
                    needs.Add((Math.Min(message.From, message.To), Math.Max(message.From, message.To), TextMetrics.Width(message.Text) + 32));
                    break;
                case SequenceNote { Placement: NotePlacement.RightOf } note:
                    needs.Add((note.First, note.First + 1, NoteWidth(note) + 2 * NoteGap));
                    break;
                case SequenceNote { Placement: NotePlacement.LeftOf } note:
                    needs.Add((note.First - 1, note.First, NoteWidth(note) + 2 * NoteGap));
                    break;
            }
        }

        // Narrow spans first, so a wide span only adds what its narrower parts have not already provided.
        foreach (var (low, high, distance) in needs.Where(need => need.Low >= 0 && need.High < count).OrderBy(need => need.High - need.Low))
        {
            var current = 0.0;
            for (var index = low; index < high; index++)
            {
                current += gaps[index];
            }
            if (current < distance)
            {
                var extra = (distance - current) / (high - low);
                for (var index = low; index < high; index++)
                {
                    gaps[index] += extra;
                }
            }
        }

        var centers = new double[count];
        for (var index = 1; index < count; index++)
        {
            centers[index] = centers[index - 1] + gaps[index - 1];
        }
        return centers;
    }

    private static double Message(List<Action> steps, DrawingBuilder builder, SequenceMessage message, double from, double to, double top)
    {
        var labelHeight = HasText(message.Text) ? TextMetrics.Height(message.Text) : 0;
        var lineY = top + labelHeight + LabelGap;
        steps.Add(() =>
        {
            if (labelHeight > 0)
            {
                builder.Text((from + to) / 2, top + labelHeight / 2, message.Text);
            }
            builder.Connector([new(from, lineY), new(to, lineY)], DrawingBuilder.LineThickness, message.Dotted ? 3 : 0, message.Arrow);
        });
        return lineY + StepGap;
    }

    private static double SelfMessage(List<Action> steps, DrawingBuilder builder, SequenceMessage message, double center, double top)
    {
        var labelHeight = HasText(message.Text) ? TextMetrics.Height(message.Text) : 0;
        var overhang = Math.Max(0, (labelHeight - LoopHeight) / 2);
        var y1 = top + overhang + 4;
        var y2 = y1 + LoopHeight;
        steps.Add(() =>
        {
            builder.Connector(
                [new(center, y1), new(center + LoopReach, y1), new(center + LoopReach, y2), new(center, y2)],
                DrawingBuilder.LineThickness, message.Dotted ? 3 : 0, message.Arrow);
            if (labelHeight > 0)
            {
                builder.TextFrom(center + LoopReach + LabelGap, (y1 + y2) / 2, message.Text);
            }
        });
        return y2 + overhang + StepGap;
    }

    private static double Note(List<Action> steps, DrawingBuilder builder, SequenceNote note, double[] centers, double top)
    {
        var width = note.Placement == NotePlacement.Over && note.First != note.Last
            ? Math.Max(NoteWidth(note), centers[note.Last] - centers[note.First] + 2 * SpanOverhang)
            : NoteWidth(note);
        var height = TextMetrics.Height(note.Text) + 2 * NotePadding - 8;
        var left = note.Placement switch
        {
            NotePlacement.RightOf => centers[note.First] + NoteGap,
            NotePlacement.LeftOf => centers[note.First] - NoteGap - width,
            _ => (centers[note.First] + centers[note.Last]) / 2 - width / 2,
        };
        var boxTop = top + 2;
        steps.Add(() =>
        {
            builder.Rectangle(left, boxTop, width, height, 0);
            builder.Text(left + width / 2, boxTop + height / 2, note.Text);
        });
        return boxTop + height + StepGap;
    }

    private static double NoteWidth(SequenceNote note) => TextMetrics.Width(note.Text) + 2 * NotePadding;

    private static bool HasText(IReadOnlyList<string> lines) => lines.Any(line => line.Length > 0);
}
