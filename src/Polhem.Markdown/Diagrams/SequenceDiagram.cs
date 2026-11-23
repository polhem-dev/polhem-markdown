namespace Polhem.Markdown.Diagrams;

// NOTE: the parsed form of a sequence diagram is a group of small records used only between SequenceParser and
// SequenceLayout, so they share this file.

/// <summary>Where a note stands relative to the participants it names.</summary>
internal enum NotePlacement
{
    LeftOf,
    RightOf,
    Over,
}

/// <summary>A participant, with its display name split into lines.</summary>
internal sealed record SequenceParticipant(string Id, IReadOnlyList<string> Lines);

/// <summary>A message or a note, in the order the diagram lists them, with the text it shows split into lines.</summary>
internal abstract record SequenceStep(IReadOnlyList<string> Text);

/// <summary>A message between participants, given by their indexes; <c>From == To</c> for a message to itself.</summary>
internal sealed record SequenceMessage(int From, int To, bool Dotted, bool Arrow, IReadOnlyList<string> Text) : SequenceStep(Text);

/// <summary>A note beside or over one participant, or over the span from <paramref name="First"/> to <paramref name="Last"/>.</summary>
internal sealed record SequenceNote(NotePlacement Placement, int First, int Last, IReadOnlyList<string> Text) : SequenceStep(Text);

/// <summary>A parsed sequence diagram.</summary>
internal sealed record SequenceDiagram(IReadOnlyList<SequenceParticipant> Participants, IReadOnlyList<SequenceStep> Steps);
