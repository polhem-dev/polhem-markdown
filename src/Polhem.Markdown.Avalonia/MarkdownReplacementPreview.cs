using Avalonia.Threading;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A replacement shown in <see cref="MarkdownEditor"/> before it is made, made by
/// <see cref="MarkdownEditor.PreviewReplace"/>: the original range struck through, followed by the proposed text, until
/// the host accepts or rejects it.
/// </summary>
/// <remarks>
/// <para>
/// Accepting leaves one step in the undo history, which turns the original range into the proposed text; rejecting leaves
/// none. Edits made elsewhere while the preview is pending keep their own steps either way.
/// </para>
/// <para>
/// The proposed text cannot be edited while the preview is pending. The original range can, and accepting replaces what it
/// holds then.
/// </para>
/// </remarks>
public sealed class MarkdownReplacementPreview
{
    private readonly MarkdownEditor _editor;
    private readonly PreviewSpan _initialSpan;
    private PreviewSpan _span;

    internal MarkdownReplacementPreview(MarkdownEditor editor, int start, int length, string text)
    {
        _editor = editor;
        Text = text;
        _initialSpan = new PreviewSpan(start, length, text.Length);
        _span = _initialSpan;
    }

    /// <summary>
    /// Occurs when <see cref="IsPending"/> changes: when the preview is accepted or rejected, ends for another reason, or
    /// comes back because the user redid the step that undid it.
    /// </summary>
    public event EventHandler? PendingChanged;

    /// <summary>Gets the proposed text.</summary>
    public string Text { get; }

    /// <summary>Gets where the original range starts now, as an offset in <see cref="MarkdownEditor.Markdown"/>.</summary>
    /// <remarks>Once the preview is no longer pending, it keeps where the range was then.</remarks>
    public int Start => _span.OriginalStart;

    /// <summary>Gets how many UTF-16 code units the original range covers now.</summary>
    /// <remarks>Once the preview is no longer pending, it keeps how long the range was then.</remarks>
    public int Length => _span.OriginalLength;

    /// <summary>
    /// Gets where the proposed text starts now, as an offset in <see cref="MarkdownEditor.Markdown"/>: right after the
    /// original range. It covers <see cref="Text"/>'s length.
    /// </summary>
    public int ProposedStart => _span.ProposedStart;

    /// <summary>Gets whether the preview is shown and waits to be accepted or rejected.</summary>
    /// <remarks>
    /// <para>
    /// It becomes <see langword="false"/> for good when the preview is accepted or rejected, when
    /// <see cref="MarkdownEditor.Markdown"/> is set, when a change from code alters the proposed text, which leaves that
    /// text in place as an ordinary edit, or when another preview starts.
    /// </para>
    /// <para>
    /// Undoing the step that showed the preview makes it <see langword="false"/> too, and redoing that step makes it
    /// <see langword="true"/> again, until an edit clears what there is to redo.
    /// </para>
    /// </remarks>
    public bool IsPending => IsApplied && !IsEnded;

    /// <summary>Whether the proposed text is in the document: not undone.</summary>
    internal bool IsApplied { get; private set; } = true;

    /// <summary>Whether the preview is over and cannot come back.</summary>
    internal bool IsEnded { get; private set; }

    /// <summary>Where the preview is now, followed through the edits.</summary>
    internal PreviewSpan Span => _span;

    /// <summary>Where the preview was when its text was inserted, which redoing that step returns it to.</summary>
    internal PreviewSpan InitialSpan => _initialSpan;

    /// <summary>
    /// Makes the replacement: the original range becomes the proposed text, as one step to undo.
    /// </summary>
    /// <returns>Whether the preview was pending; if not, nothing changes.</returns>
    public bool Accept() => _editor.ResolvePreview(this, accept: true);

    /// <summary>Removes the proposed text, leaving nothing in the undo history.</summary>
    /// <returns>Whether the preview was pending; if not, nothing changes.</returns>
    public bool Reject() => _editor.ResolvePreview(this, accept: false);

    /// <summary>Follows a change outside the proposed text.</summary>
    internal void Follow(int offset, int removed, int inserted) => _span.Apply(offset, removed, inserted);

    /// <summary>Ends the preview for good, and tells the host if it was pending or has just been resolved.</summary>
    /// <param name="resolved">Whether the preview was just accepted or rejected.</param>
    /// <param name="inDocumentChange">
    /// Whether a change of the document is in progress, which the host's handler could not change the text during.
    /// </param>
    internal void End(bool resolved = false, bool inDocumentChange = false)
    {
        if (IsEnded)
        {
            return;
        }
        var wasPending = IsPending;
        IsEnded = true;
        _editor.OnPreviewPendingChanged(this);
        if (!wasPending && !resolved)
        {
            return;
        }
        if (inDocumentChange)
        {
            RaiseLater();
        }
        else
        {
            PendingChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal void OnInsertionUndone()
    {
        if (IsEnded || !IsApplied)
        {
            return;
        }
        IsApplied = false;
        _editor.OnPreviewPendingChanged(this);
        if (!_editor.IsResolvingPreview)
        {
            RaiseLater();
        }
    }

    internal void OnInsertionRedone()
    {
        if (IsEnded || IsApplied)
        {
            return;
        }
        IsApplied = true;
        _span = _initialSpan;
        _editor.OnPreviewPendingChanged(this);
        RaiseLater();
    }

    /// <summary>
    /// Raises <see cref="PendingChanged"/> once the change, undo or redo in progress is over, since the host may change the
    /// text in its handler, which AvaloniaEdit does not allow during one.
    /// </summary>
    private void RaiseLater() => Dispatcher.UIThread.Post(() => PendingChanged?.Invoke(this, EventArgs.Empty));
}
