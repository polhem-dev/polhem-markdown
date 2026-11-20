namespace Polhem.Markdown.Model;

/// <summary>
/// Where a node came from in the Markdown source, as a start offset and a length in UTF-16 code units. A viewer compares
/// it with the source to find the blocks that changed, and an editor uses it to map the preview back to the source.
/// </summary>
internal readonly record struct SourceRange(int Start, int Length)
{
    public int End => Start + Length;
}
