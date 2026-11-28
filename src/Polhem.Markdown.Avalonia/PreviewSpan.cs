namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Where a <see cref="MarkdownReplacementPreview"/> is in the text: the original range, followed at once by the proposed
/// text, which no edit may change.
/// </summary>
/// <remarks>
/// It follows the edits made around the proposed text, and tells where each of them lands in the text without the
/// proposed text, which is where the editor makes them again when the preview is resolved.
/// </remarks>
internal struct PreviewSpan(int originalStart, int originalLength, int proposedLength)
{
    /// <summary>Gets where the original range starts.</summary>
    public int OriginalStart { get; private set; } = originalStart;

    /// <summary>Gets where the proposed text starts, which is also where the original range ends.</summary>
    public int ProposedStart { get; private set; } = originalStart + originalLength;

    /// <summary>Gets how long the proposed text is. No edit changes it.</summary>
    public readonly int ProposedLength { get; } = proposedLength;

    /// <summary>Gets how long the original range is.</summary>
    public readonly int OriginalLength => ProposedStart - OriginalStart;

    /// <summary>Gets where the proposed text ends.</summary>
    public readonly int ProposedEnd => ProposedStart + ProposedLength;

    /// <summary>
    /// Whether a change of <paramref name="removed"/> characters at <paramref name="offset"/> would change the proposed
    /// text. Text inserted where the proposed text starts or ends is outside it.
    /// </summary>
    public readonly bool Touches(int offset, int removed) =>
        ProposedLength > 0 && offset + removed > ProposedStart && offset < ProposedEnd;

    /// <summary>
    /// Follows a change that leaves the proposed text alone: <paramref name="removed"/> characters at
    /// <paramref name="offset"/> replaced by <paramref name="inserted"/> characters.
    /// </summary>
    /// <returns>Where the change is in the text without the proposed text.</returns>
    public int Apply(int offset, int removed, int inserted)
    {
        var mapped = offset >= ProposedEnd && ProposedLength > 0 ? offset - ProposedLength : offset;
        var start = Move(OriginalStart, offset, removed, inserted);
        ProposedStart = Move(ProposedStart, offset, removed, inserted);
        OriginalStart = Math.Min(start, ProposedStart);
        return mapped;
    }

    /// <summary>
    /// Moves a point between characters with a change. Text inserted at the point goes before it, and a point inside the
    /// removed text goes after the text that replaces it, unless the removal starts at the point.
    /// </summary>
    private static int Move(int point, int offset, int removed, int inserted)
    {
        if (point < offset || (point == offset && removed > 0))
        {
            return point;
        }
        return point <= offset + removed ? offset + inserted : point + inserted - removed;
    }
}
