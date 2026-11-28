using AvaloniaEdit.Document;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Sits in the undo history in the same step as the text a <see cref="MarkdownReplacementPreview"/> inserted, so the
/// preview knows when that step is undone or redone.
/// </summary>
/// <remarks>
/// It changes no text: AvaloniaEdit does not let an operation of its own change the document while it undoes or redoes.
/// Undoing the step undoes this first and the insertion after; redoing it redoes the insertion first.
/// </remarks>
internal sealed class PreviewUndoMarker(MarkdownReplacementPreview preview) : IUndoableOperation
{
    public void Undo() => preview.OnInsertionUndone();

    public void Redo() => preview.OnInsertionRedone();
}
