using Avalonia;
using Avalonia.Media;
using AvaloniaEdit.Document;

namespace Polhem.Markdown.Avalonia;

// A replacement shown before it is made, which the host accepts or rejects, leaving only the net change in the undo
// history.
public partial class MarkdownEditor
{
    /// <summary>Defines the <see cref="PreviewRemovedBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> PreviewRemovedBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(PreviewRemovedBrush), new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)));

    /// <summary>Defines the <see cref="PreviewAddedBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> PreviewAddedBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(PreviewAddedBrush), new SolidColorBrush(Color.FromRgb(0x22, 0xA3, 0x5A)));

    /// <summary>How opaque the background behind a preview's text is, so the text stays readable over any brush.</summary>
    private const double PreviewBackgroundOpacity = 0.25;

    /// <summary>The marks that show the pending preview, drawn by a renderer of their own.</summary>
    private readonly TextSegmentCollection<RangeMarkSegment> _previewMarks = [];

    private readonly PreviewReadOnlySections _previewSections;

    /// <summary>The latest preview: pending, or undone and waiting for a redo that may bring it back.</summary>
    private MarkdownReplacementPreview? _preview;

    /// <summary>The changes the undo steps make while a preview is resolved, collected for the step being undone.</summary>
    private List<UndoneChange>? _undoneChanges;

    /// <summary>
    /// Gets or sets the brush of the original range of a pending preview: drawn as a strikethrough, and translucent behind
    /// the text.
    /// </summary>
    public IBrush PreviewRemovedBrush
    {
        get => GetValue(PreviewRemovedBrushProperty);
        set => SetValue(PreviewRemovedBrushProperty, value);
    }

    /// <summary>Gets or sets the brush drawn translucent behind the proposed text of a pending preview.</summary>
    public IBrush PreviewAddedBrush
    {
        get => GetValue(PreviewAddedBrushProperty);
        set => SetValue(PreviewAddedBrushProperty, value);
    }

    /// <summary>The marks that draw the pending preview, for the tests.</summary>
    internal IReadOnlyList<MarkdownRangeMark> PreviewMarks => [.. _previewMarks.Select(segment => segment.Mark)];

    /// <summary>Whether a preview is being accepted or rejected, which replays steps of the undo history.</summary>
    internal bool IsResolvingPreview { get; private set; }

    /// <summary>Where the pending preview is, or <see langword="null"/> when none is pending.</summary>
    internal PreviewSpan? PendingPreviewSpan => _preview is { IsPending: true } preview ? preview.Span : null;

    /// <summary>
    /// Shows a replacement of <paramref name="length"/> characters from <paramref name="start"/> with
    /// <paramref name="text"/> before making it: the range struck through, followed by <paramref name="text"/>, until the
    /// host accepts or rejects it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="text"/> is inserted into the document right after the range, so <see cref="Markdown"/> holds it
    /// while the preview is pending; <see cref="GetMarkdownWithoutPreview"/> gives the text without it, for a host that
    /// saves meanwhile. The user cannot edit the inserted text, but can edit anywhere else, the range included.
    /// </para>
    /// <para>
    /// <see cref="MarkdownReplacementPreview.Accept"/> leaves one step in the undo history, which turns the range into
    /// <paramref name="text"/>, and <see cref="MarkdownReplacementPreview.Reject"/> leaves none. The edits made elsewhere
    /// while the preview is pending keep their own steps. Undoing past the edits made since the preview started removes it
    /// until it is redone.
    /// </para>
    /// <para>
    /// One preview is pending at a time. It changes the text when the editor is read-only too, as
    /// <see cref="Replace(int, int, string)"/> does. <see cref="PreviewRemovedBrush"/> and <see cref="PreviewAddedBrush"/>
    /// draw it.
    /// </para>
    /// </remarks>
    /// <param name="start">Where the range starts, as an offset in <see cref="Markdown"/>.</param>
    /// <param name="length">How many UTF-16 code units the range covers: 0 to insert.</param>
    /// <param name="text">The text that would take the range's place: empty to delete.</param>
    /// <returns>The preview, which the host accepts or rejects.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The range is not within the text.</exception>
    /// <exception cref="ArgumentException">The range and <paramref name="text"/> are both empty.</exception>
    /// <exception cref="InvalidOperationException">Another preview is pending.</exception>
    public MarkdownReplacementPreview PreviewReplace(int start, int length, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var document = _editor.Document;
        CheckRange(start, length, document.TextLength);
        if (length == 0 && text.Length == 0)
        {
            throw new ArgumentException("The replacement changes nothing: the range and the text are both empty.", nameof(text));
        }
        if (_preview is { IsPending: true })
        {
            throw new InvalidOperationException("Another preview is pending. Accept or reject it first.");
        }

        // A preview that was undone could come back with a redo, which the insertion below clears.
        _preview?.End();
        var preview = new MarkdownReplacementPreview(this, start, length, text);
        RunBatch(() =>
        {
            // NOTE: the marker shares the insertion's undo step, so the preview hears when that step is undone or redone.
            document.BeginUpdate();
            try
            {
                document.Insert(start + length, text);
                document.UndoStack.Push(new PreviewUndoMarker(preview));
            }
            finally
            {
                document.EndUpdate();
            }
        });
        _preview = preview;
        OnPreviewPendingChanged(preview);
        return preview;
    }

    /// <summary>
    /// Gets <see cref="Markdown"/> without the proposed text of the pending preview: the text as it would be if the preview
    /// were rejected now.
    /// </summary>
    /// <returns>The text without the proposed text, or <see cref="Markdown"/>'s text when no preview is pending.</returns>
    public string GetMarkdownWithoutPreview()
    {
        var text = _editor.Document.Text;
        return PendingPreviewSpan is { ProposedLength: > 0 } span ? text.Remove(span.ProposedStart, span.ProposedLength) : text;
    }

    /// <summary>
    /// Accepts or rejects <paramref name="preview"/>, rewriting the undo history from where it started so it holds the
    /// edits made since, without the proposed text, and then, on accepting, the replacement as one step.
    /// </summary>
    /// <remarks>
    /// AvaloniaEdit records every change with its offsets in the text at that time, and cannot drop a step from the middle
    /// of its history. So the steps since the preview started are undone, back to the one that inserted the proposed text,
    /// and the edits they made are made again where they land without that text, one step each.
    /// </remarks>
    internal bool ResolvePreview(MarkdownReplacementPreview preview, bool accept)
    {
        if (!preview.IsPending || preview != _preview)
        {
            return false;
        }
        var document = _editor.Document;
        var undoStack = document.UndoStack;
        var live = preview.Span;
        var selectionStart = WithoutProposed(_editor.SelectionStart, live);
        var selectionEnd = WithoutProposed(_editor.SelectionStart + _editor.SelectionLength, live);

        IsResolvingPreview = true;
        try
        {
            RunBatch(() =>
            {
                var steps = new List<List<UndoneChange>>();
                while (preview.IsApplied)
                {
                    if (!undoStack.CanUndo)
                    {
                        throw new InvalidOperationException("The undo history no longer holds the step that showed the preview.");
                    }
                    _undoneChanges = [];
                    undoStack.Undo();
                    steps.Add(_undoneChanges);
                }
                _undoneChanges = null;

                // The last step undone is the one that inserted the proposed text.
                steps.RemoveAt(steps.Count - 1);
                undoStack.ClearRedoStack();

                var span = preview.InitialSpan;
                for (var i = steps.Count - 1; i >= 0; i--)
                {
                    RedoWithoutProposed(document, steps[i], ref span);
                }
                if (accept)
                {
                    var (start, length) = (span.OriginalStart, span.OriginalLength);
                    document.Replace(start, length, preview.Text);
                    selectionStart = AfterReplacement(selectionStart, start, length, preview.Text.Length);
                    selectionEnd = AfterReplacement(selectionEnd, start, length, preview.Text.Length);
                }
                var textLength = document.TextLength;
                selectionStart = Math.Min(selectionStart, textLength);
                _editor.Select(selectionStart, Math.Clamp(selectionEnd, selectionStart, textLength) - selectionStart);
            });
        }
        finally
        {
            _undoneChanges = null;
            IsResolvingPreview = false;
        }

        preview.End(resolved: true);
        RemoveEmptyMarks();
        KeepFind();
        OnTextEdited();
        return true;
    }

    /// <summary>Called by <paramref name="preview"/> when it becomes pending or stops being pending.</summary>
    internal void OnPreviewPendingChanged(MarkdownReplacementPreview preview)
    {
        if (preview != _preview)
        {
            return;
        }
        if (preview.IsEnded)
        {
            _preview = null;
        }
        UpdatePreviewMarks();
    }

    /// <summary>Follows a change of the document with the pending preview, from <see cref="OnDocumentChanged"/>.</summary>
    private void OnDocumentChangedForPreview(DocumentChangeEventArgs e)
    {
        _undoneChanges?.Add(new UndoneChange(e.Offset, e.RemovedText.Text, e.InsertedText.Text));
        if (IsResolvingPreview || _preview is not { IsPending: true } preview)
        {
            return;
        }

        // NOTE: an undo or a redo only replays an edit the preview already followed, so only a new edit can change the
        // proposed text; the user cannot, so it comes from code.
        if (_editor.Document.UndoStack.AcceptChanges && preview.Span.Touches(e.Offset, e.RemovalLength))
        {
            preview.End(inDocumentChange: true);
            return;
        }
        preview.Follow(e.Offset, e.RemovalLength, e.InsertionLength);
        UpdatePreviewMarks();
    }

    /// <summary>Ends the latest preview for good, when the text is replaced from <see cref="Markdown"/>.</summary>
    private void EndPreview() => _preview?.End();

    private void OnPreviewPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == PreviewRemovedBrushProperty || change.Property == PreviewAddedBrushProperty)
        {
            UpdatePreviewMarks();
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            ApplyPreviewReadOnlySections();
        }
    }

    private void UpdatePreviewMarks()
    {
        _previewMarks.Clear();
        if (PendingPreviewSpan is { } span)
        {
            if (span.OriginalLength > 0)
            {
                AddPreviewMark(span.OriginalStart, span.OriginalLength, MarkdownRangeMarkStyle.Background, PreviewRemovedBrush);
                AddPreviewMark(span.OriginalStart, span.OriginalLength, MarkdownRangeMarkStyle.Strikethrough, PreviewRemovedBrush);
            }
            if (span.ProposedLength > 0)
            {
                AddPreviewMark(span.ProposedStart, span.ProposedLength, MarkdownRangeMarkStyle.Background, PreviewAddedBrush);
            }
        }
        RedrawMarks();
    }

    private void AddPreviewMark(int start, int length, MarkdownRangeMarkStyle style, IBrush brush) =>
        _previewMarks.Add(new MarkdownRangeMark(start, length, style, brush, owner: null).Segment);

    /// <summary>
    /// Keeps the user out of the proposed text of a pending preview, unless the whole editor is read-only, which AvaloniaEdit
    /// enforces with a provider of its own and replaces with one that allows everything when it stops being read-only.
    /// </summary>
    private void ApplyPreviewReadOnlySections()
    {
        if (!IsReadOnly)
        {
            _editor.TextArea.ReadOnlySectionProvider = _previewSections;
        }
    }

    /// <summary>
    /// Makes the edits of an undone step again, as one step, where they land in the text without the proposed text.
    /// </summary>
    /// <param name="document">The document, now without the proposed text.</param>
    /// <param name="undone">The changes the undo made, in the order it made them.</param>
    /// <param name="span">Where the preview was before the step, moved to where it is after it.</param>
    private static void RedoWithoutProposed(TextDocument document, List<UndoneChange> undone, ref PreviewSpan span)
    {
        document.BeginUpdate();
        try
        {
            for (var i = undone.Count - 1; i >= 0; i--)
            {
                // The edit the undo reversed removed what the undo inserted, and inserted what it removed.
                var change = undone[i];
                var offset = span.Apply(change.Offset, change.Inserted.Length, change.Removed.Length);
                document.Replace(offset, change.Inserted.Length, change.Removed);
            }
        }
        finally
        {
            document.EndUpdate();
        }
    }

    /// <summary>Where <paramref name="offset"/> lands once the proposed text in <paramref name="span"/> is removed.</summary>
    private static int WithoutProposed(int offset, PreviewSpan span)
    {
        if (offset <= span.ProposedStart)
        {
            return offset;
        }
        return offset < span.ProposedEnd ? span.ProposedStart : offset - span.ProposedLength;
    }

    /// <summary>
    /// Where <paramref name="offset"/> lands once <paramref name="length"/> characters from <paramref name="start"/> are
    /// replaced by <paramref name="inserted"/> characters: after them, if it was within the range or at its end.
    /// </summary>
    private static int AfterReplacement(int offset, int start, int length, int inserted)
    {
        if (offset <= start)
        {
            return offset;
        }
        return offset >= start + length ? offset + inserted - length : start + inserted;
    }

    /// <summary>A change an undo made: <see cref="Removed"/> replaced by <see cref="Inserted"/> at <see cref="Offset"/>.</summary>
    private readonly record struct UndoneChange(int Offset, string Removed, string Inserted);
}
