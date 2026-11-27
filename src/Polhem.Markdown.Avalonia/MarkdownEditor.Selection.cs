using Avalonia.Threading;

namespace Polhem.Markdown.Avalonia;

// The selection, and changes to ranges of the text from code, for a host that works on part of the text.
public partial class MarkdownEditor
{
    /// <summary>The caret and the selection last reported by <see cref="SelectionChanged"/>.</summary>
    private (int Caret, int Start, int Length) _reportedSelection;

    /// <summary>
    /// How many changes made from code are in progress. While any is, the caret and the selection pass through
    /// positions the host does not need to hear about, so <see cref="SelectionChanged"/> waits until the last ends.
    /// </summary>
    private int _selectionBatch;

    /// <summary>Whether a report of the selection is posted to the dispatcher and has not run yet.</summary>
    private bool _selectionReportPending;

    /// <summary>
    /// Occurs when the caret moves or the selection changes, whether by the user, by an edit, or from code.
    /// </summary>
    /// <remarks>
    /// <see cref="Select"/>, <see cref="Replace(IEnumerable{MarkdownReplacement})"/> and
    /// <see cref="ApplyFormat(MarkdownFormat)"/> raise it once at most, before they return. Other changes, such as
    /// typing, a key that extends the selection, or setting <see cref="CaretOffset"/>, raise it once on the UI thread
    /// shortly after, so one key press is one event and not one for the caret and another for the selection.
    /// </remarks>
    public event EventHandler? SelectionChanged;

    /// <summary>Gets where the selection starts, as an offset in <see cref="Markdown"/>.</summary>
    /// <remarks>With nothing selected, it is <see cref="CaretOffset"/>.</remarks>
    public int SelectionStart => _editor.SelectionStart;

    /// <summary>Gets how many UTF-16 code units the selection covers: 0 when nothing is selected.</summary>
    public int SelectionLength => _editor.SelectionLength;

    /// <summary>Gets the selected text, or an empty string when nothing is selected.</summary>
    public string SelectedText => _editor.SelectedText;

    /// <summary>
    /// Selects <paramref name="length"/> characters from <paramref name="start"/>, puts the caret at the end of them, and
    /// scrolls them into view.
    /// </summary>
    /// <remarks>A length of 0 clears the selection and moves the caret to <paramref name="start"/>.</remarks>
    /// <param name="start">Where the selection starts, as an offset in <see cref="Markdown"/>.</param>
    /// <param name="length">How many UTF-16 code units to select.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is not within the text.</exception>
    public void Select(int start, int length)
    {
        CheckRange(start, length, _editor.Document.TextLength);
        RunBatch(() =>
        {
            _editor.Select(start, length);
            _editor.TextArea.Caret.BringCaretToView();
        });
    }

    /// <summary>
    /// Replaces <paramref name="length"/> characters from <paramref name="start"/> with <paramref name="text"/>, as one
    /// step to undo.
    /// </summary>
    /// <remarks>
    /// The caret and the selection follow the change as they do when the user types, and the view does not scroll. It
    /// changes the text when the editor is read-only too, as setting <see cref="Markdown"/> does; unlike that, it keeps
    /// the undo history.
    /// </remarks>
    /// <param name="start">Where the range starts, as an offset in <see cref="Markdown"/>.</param>
    /// <param name="length">How many UTF-16 code units the range covers: 0 to insert.</param>
    /// <param name="text">The text that takes the range's place: empty to delete.</param>
    /// <exception cref="ArgumentOutOfRangeException">The range is not within the text.</exception>
    public void Replace(int start, int length, string text) => Replace([new MarkdownReplacement(start, length, text)]);

    /// <summary>
    /// Makes <paramref name="replacements"/> together, as one step to undo, with every offset in the text as it is before
    /// any of them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ranges may come in any order but must not overlap: a range may end where the next starts, and an insertion may
    /// sit at the start or the end of a range, but two insertions at the same offset are rejected, because their order
    /// would be a guess. Nothing changes when one is rejected.
    /// </para>
    /// <para>
    /// The caret and the selection follow the changes as they do when the user types, and the view does not scroll. It
    /// changes the text when the editor is read-only too, as setting <see cref="Markdown"/> does; unlike that, it keeps
    /// the undo history. <see cref="Markdown"/> and <see cref="SelectionChanged"/> report the result once.
    /// </para>
    /// </remarks>
    /// <param name="replacements">The changes to make.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="replacements"/>, one of them, or the text of one is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">A range is not within the text.</exception>
    /// <exception cref="ArgumentException">Two ranges overlap, or two insertions are at the same offset.</exception>
    public void Replace(IEnumerable<MarkdownReplacement> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        var ordered = Order(replacements, _editor.Document.TextLength);
        if (ordered.Count == 0)
        {
            return;
        }
        RunBatch(() =>
        {
            var document = _editor.Document;
            document.BeginUpdate();
            try
            {
                // From the end backwards, so every offset still points into the text as it was before the changes.
                for (var i = ordered.Count - 1; i >= 0; i--)
                {
                    var replacement = ordered[i];
                    document.Replace(replacement.Start, replacement.Length, replacement.Text);
                }
            }
            finally
            {
                document.EndUpdate();
            }
        });
    }

    /// <summary>
    /// Checks <paramref name="replacements"/> against a text of <paramref name="textLength"/> characters and sorts them by
    /// where they start, insertions before the range they start.
    /// </summary>
    internal static List<MarkdownReplacement> Order(IEnumerable<MarkdownReplacement> replacements, int textLength)
    {
        var ordered = new List<MarkdownReplacement>();
        foreach (var replacement in replacements)
        {
            if (replacement?.Text is null)
            {
                throw new ArgumentNullException(nameof(replacements), "A replacement, or its text, is null.");
            }
            CheckRange(replacement.Start, replacement.Length, textLength);
            ordered.Add(replacement);
        }
        ordered.Sort((a, b) => a.Start != b.Start ? a.Start.CompareTo(b.Start) : a.Length.CompareTo(b.Length));
        for (var i = 1; i < ordered.Count; i++)
        {
            var previous = ordered[i - 1];
            var current = ordered[i];
            if (current.Start < previous.Start + previous.Length || (current.Start == previous.Start && current.Length == 0))
            {
                throw new ArgumentException(
                    $"The replacement at {current.Start} (length {current.Length}) overlaps the one at {previous.Start} (length {previous.Length}).",
                    nameof(replacements));
            }
        }
        return ordered;
    }

    private static void CheckRange(int start, int length, int textLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, textLength);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(length, textLength - start);
    }

    /// <summary>Runs <paramref name="change"/>, then raises <see cref="SelectionChanged"/> once if it moved anything.</summary>
    private void RunBatch(Action change)
    {
        _selectionBatch++;
        try
        {
            change();
        }
        finally
        {
            _selectionBatch--;
        }
        ReportSelection();
    }

    private void ScheduleSelectionReport()
    {
        if (_selectionBatch > 0 || _selectionReportPending)
        {
            return;
        }
        _selectionReportPending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _selectionReportPending = false;
            ReportSelection();
        });
    }

    private void ReportSelection()
    {
        if (_selectionBatch > 0)
        {
            return;
        }
        var selection = (_editor.CaretOffset, _editor.SelectionStart, _editor.SelectionLength);
        if (selection != _reportedSelection)
        {
            _reportedSelection = selection;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
