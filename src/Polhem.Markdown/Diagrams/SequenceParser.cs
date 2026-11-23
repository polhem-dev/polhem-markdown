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
        var builder = new Builder();
        foreach (var raw in lines)
        {
            var line = raw.Trim().TrimEnd(';').Trim();
            if (line.Length > 0 && !builder.TryLine(line))
            {
                return false;
            }
        }
        return builder.TryBuild(out diagram);
    }

    private static NotePlacement Placement(string text)
    {
        if (text.StartsWith("left", StringComparison.OrdinalIgnoreCase))
        {
            return NotePlacement.LeftOf;
        }
        return text.StartsWith("right", StringComparison.OrdinalIgnoreCase) ? NotePlacement.RightOf : NotePlacement.Over;
    }

    /// <summary>Collects the participants and steps of one diagram, one line at a time.</summary>
    private sealed class Builder
    {
        private readonly List<(string Id, string Name)> _participants = [];

        // NOTE: the default comparer of `string` is ordinal, which participant ids need.
        private readonly Dictionary<string, int> _indexes = [];
        private readonly List<SequenceStep> _steps = [];

        public bool TryLine(string line)
        {
            if (_steps.Count == MaxSteps)
            {
                return false;
            }
            if (ParticipantPattern().Match(line) is { Success: true } declaration)
            {
                return TryDeclare(declaration);
            }
            if (MessagePattern().Match(line) is { Success: true } message)
            {
                return TryMessage(message);
            }
            if (NotePattern().Match(line) is { Success: true } note)
            {
                return TryNote(note);
            }
            return false;
        }

        public bool TryBuild(out SequenceDiagram diagram)
        {
            diagram = new SequenceDiagram(
                _participants.Select(participant => new SequenceParticipant(participant.Id, TextMetrics.Lines(participant.Name))).ToList(),
                _steps);
            return _participants.Count > 0;
        }

        private bool TryDeclare(Match declaration)
        {
            var id = declaration.Groups["id"].Value;
            var name = declaration.Groups["alias"].Success ? declaration.Groups["alias"].Value.Trim() : id;
            if (_indexes.TryGetValue(id, out var existing))
            {
                _participants[existing] = (id, name);
                return true;
            }
            return TryAdd(id, name);
        }

        private bool TryMessage(Match message)
        {
            if (!TryIndex(message.Groups["from"].Value, out var from) || !TryIndex(message.Groups["to"].Value, out var to))
            {
                return false;
            }
            var arrow = message.Groups["arrow"].Value;
            var label = TextMetrics.Lines(message.Groups["text"].Value.Trim());
            _steps.Add(new SequenceMessage(from, to, arrow.StartsWith("--", StringComparison.Ordinal), arrow.EndsWith(">>", StringComparison.Ordinal), label));
            return true;
        }

        private bool TryNote(Match note)
        {
            var placement = Placement(note.Groups["placement"].Value);
            var names = note.Groups["names"].Value.Split(',', StringSplitOptions.TrimEntries);
            var allowed = placement == NotePlacement.Over ? 2 : 1;
            if (names.Length > allowed || !TryIndex(names[0], out var first))
            {
                return false;
            }
            var last = first;
            if (names.Length == 2 && !TryIndex(names[1], out last))
            {
                return false;
            }
            var text = TextMetrics.Lines(note.Groups["text"].Value.Trim());
            _steps.Add(new SequenceNote(placement, Math.Min(first, last), Math.Max(first, last), text));
            return true;
        }

        private bool TryIndex(string id, out int index)
        {
            if (_indexes.TryGetValue(id, out index))
            {
                return true;
            }
            index = _participants.Count;
            return id.Length > 0 && TryAdd(id, id);
        }

        private bool TryAdd(string id, string name)
        {
            if (_participants.Count == MaxParticipants)
            {
                return false;
            }
            _indexes.Add(id, _participants.Count);
            _participants.Add((id, name));
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
