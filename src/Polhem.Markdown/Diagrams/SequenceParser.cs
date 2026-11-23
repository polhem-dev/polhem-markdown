using System.Text.RegularExpressions;

namespace Polhem.Markdown.Diagrams;

/// <summary>
/// Parses the supported subset of a Mermaid sequence diagram: <c>participant</c> and <c>actor</c> declarations with an
/// optional <c>as</c> alias, messages written with <c>-&gt;</c>, <c>--&gt;</c>, <c>-&gt;&gt;</c> or <c>--&gt;&gt;</c>,
/// and notes left of, right of or over participants.
/// </summary>
/// <remarks>
/// Anything else, such as a loop or alt block, activation, autonumbering or another arrow, makes the whole parse fail, so
/// the diagram shows as its code instead of being drawn with something missing.
/// </remarks>
internal static partial class SequenceParser
{
    /// <summary>The most participants a diagram may have before it shows as code.</summary>
    public const int MaxParticipants = 40;

    /// <summary>The most messages and notes a diagram may have before it shows as code.</summary>
    public const int MaxSteps = 500;

    public static bool TryParse(IEnumerable<string> lines, out SequenceDiagram diagram)
    {
        diagram = null!;
        var participants = new List<(string Id, string Name)>();
        var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
        var steps = new List<SequenceStep>();

        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimEnd(';').Trim();
            if (line.Length == 0)
            {
                continue;
            }
            if (steps.Count == MaxSteps)
            {
                return false;
            }

            if (ParticipantPattern().Match(line) is { Success: true } declaration)
            {
                var id = declaration.Groups["id"].Value;
                var name = declaration.Groups["alias"].Success ? declaration.Groups["alias"].Value.Trim() : id;
                if (indexes.TryGetValue(id, out var existing))
                {
                    participants[existing] = (id, name);
                }
                else if (!TryAdd(id, name))
                {
                    return false;
                }
            }
            else if (MessagePattern().Match(line) is { Success: true } message)
            {
                if (!TryIndex(message.Groups["from"].Value, out var from) || !TryIndex(message.Groups["to"].Value, out var to))
                {
                    return false;
                }
                var arrow = message.Groups["arrow"].Value;
                var label = TextMetrics.Lines(message.Groups["text"].Value.Trim());
                steps.Add(new SequenceMessage(from, to, arrow.StartsWith("--", StringComparison.Ordinal), arrow.EndsWith(">>", StringComparison.Ordinal), label));
            }
            else if (NotePattern().Match(line) is { Success: true } note)
            {
                var placementText = note.Groups["placement"].Value;
                var placement = placementText.StartsWith("left", StringComparison.OrdinalIgnoreCase) ? NotePlacement.LeftOf
                    : placementText.StartsWith("right", StringComparison.OrdinalIgnoreCase) ? NotePlacement.RightOf
                    : NotePlacement.Over;
                var names = note.Groups["names"].Value.Split(',', StringSplitOptions.TrimEntries);
                if (names.Length > (placement == NotePlacement.Over ? 2 : 1) || !TryIndex(names[0], out var first))
                {
                    return false;
                }
                var last = first;
                if (names.Length == 2 && !TryIndex(names[1], out last))
                {
                    return false;
                }
                var text = TextMetrics.Lines(note.Groups["text"].Value.Trim());
                var (low, high) = (Math.Min(first, last), Math.Max(first, last));
                steps.Add(new SequenceNote(placement, low, high, text));
            }
            else
            {
                return false;
            }
        }

        if (participants.Count == 0)
        {
            return false;
        }
        diagram = new SequenceDiagram(
            participants.Select(participant => new SequenceParticipant(participant.Id, TextMetrics.Lines(participant.Name))).ToList(),
            steps);
        return true;

        bool TryIndex(string id, out int index)
        {
            if (indexes.TryGetValue(id, out index))
            {
                return true;
            }
            index = participants.Count;
            return id.Length > 0 && TryAdd(id, id);
        }

        bool TryAdd(string id, string name)
        {
            if (participants.Count == MaxParticipants)
            {
                return false;
            }
            indexes.Add(id, participants.Count);
            participants.Add((id, name));
            return true;
        }
    }

    [GeneratedRegex(@"^(?:participant|actor)\s+(?<id>[^\s:;,<>+\-]+)(?:\s+as\s+(?<alias>.+))?$")]
    private static partial Regex ParticipantPattern();

    // Activation suffixes (`->>+B`) and the other arrows (`-x`, `-)`) do not match, so they fall back to code.
    [GeneratedRegex(@"^(?<from>[^\s:;,<>+\-]+)\s*(?<arrow>-->>|->>|-->|->)\s*(?<to>[^\s:;,<>+\-]+)\s*(?::(?<text>.*))?$")]
    private static partial Regex MessagePattern();

    [GeneratedRegex(@"^note\s+(?<placement>left\s+of|right\s+of|over)\s+(?<names>[^:]+?)\s*:(?<text>.*)$", RegexOptions.IgnoreCase)]
    private static partial Regex NotePattern();
}
