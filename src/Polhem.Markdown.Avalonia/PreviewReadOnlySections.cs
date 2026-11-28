using AvaloniaEdit.Document;
using AvaloniaEdit.Editing;

namespace Polhem.Markdown.Avalonia;

/// <summary>Keeps the user from typing into, or deleting, the proposed text of a pending preview.</summary>
/// <remarks>
/// It only applies to what the user does in the text area. Changes from code, such as
/// <see cref="MarkdownEditor.Replace(int, int, string)"/>, are not stopped; one that changes the proposed text ends the
/// preview instead.
/// </remarks>
internal sealed class PreviewReadOnlySections(MarkdownEditor editor) : IReadOnlySectionProvider
{
    public bool CanInsert(int offset) =>
        editor.PendingPreviewSpan is not { } span || offset <= span.ProposedStart || offset >= span.ProposedEnd;

    public IEnumerable<ISegment> GetDeletableSegments(ISegment segment)
    {
        if (editor.PendingPreviewSpan is not { } span || !span.Touches(segment.Offset, segment.Length))
        {
            yield return segment;
            yield break;
        }
        if (segment.Offset < span.ProposedStart)
        {
            yield return new TextSegment { StartOffset = segment.Offset, EndOffset = span.ProposedStart };
        }
        if (segment.EndOffset > span.ProposedEnd)
        {
            yield return new TextSegment { StartOffset = span.ProposedEnd, EndOffset = segment.EndOffset };
        }
    }
}
