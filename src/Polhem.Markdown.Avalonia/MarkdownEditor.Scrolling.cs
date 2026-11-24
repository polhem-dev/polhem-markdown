using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Polhem.Markdown.Avalonia;

// Where the editor is scrolled to, as an offset in the source, for a host that keeps the viewer in step.
public partial class MarkdownEditor
{
    /// <summary>Defines the <see cref="FirstVisibleSourceOffset"/> property.</summary>
    public static readonly DirectProperty<MarkdownEditor, int> FirstVisibleSourceOffsetProperty =
        AvaloniaProperty.RegisterDirect<MarkdownEditor, int>(nameof(FirstVisibleSourceOffset), editor => editor.FirstVisibleSourceOffset);

    private int _firstVisibleSourceOffset;

    /// <summary>
    /// The editor's scroll viewer, from its template. AvaloniaEdit keeps its own internal, and its
    /// <c>ScrollToVerticalOffset</c> does nothing.
    /// </summary>
    private ScrollViewer? _scrollViewer;

    /// <summary>
    /// Gets the offset in <see cref="Markdown"/> of the text at the top of the editor's view, moved into the top line in
    /// proportion to how much of it is scrolled past. It changes as the editor scrolls.
    /// </summary>
    public int FirstVisibleSourceOffset
    {
        get => _firstVisibleSourceOffset;
        private set => SetAndRaise(FirstVisibleSourceOffsetProperty, ref _firstVisibleSourceOffset, value);
    }

    /// <summary>
    /// Scrolls the editor so the text at <paramref name="sourceOffset"/> in <see cref="Markdown"/> is at the top of its
    /// view, or as near the top as the text allows.
    /// </summary>
    /// <remarks>The inverse of <see cref="FirstVisibleSourceOffset"/>. The caret does not move.</remarks>
    /// <param name="sourceOffset">An offset in <see cref="Markdown"/>, in UTF-16 code units.</param>
    public void ScrollToSourceOffset(int sourceOffset)
    {
        if (_scrollViewer is null)
        {
            return;
        }
        var textView = _editor.TextArea.TextView;
        var document = _editor.Document;
        var line = document.GetLineByOffset(Math.Clamp(sourceOffset, 0, document.TextLength));
        textView.EnsureVisualLines();
        var top = textView.GetVisualTopByDocumentLine(line.LineNumber);
        var height = textView.GetOrConstructVisualLine(line).Height;
        var fraction = line.Length == 0 ? 0 : (sourceOffset - line.Offset) / (double)line.Length;
        _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, top + fraction * height);
    }

    private void OnEditorTemplateApplied(object? sender, TemplateAppliedEventArgs e)
    {
        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
    }

    private void UpdateFirstVisibleSourceOffset()
    {
        var textView = _editor.TextArea.TextView;
        if (!textView.VisualLinesValid || _editor.Document.TextLength == 0)
        {
            FirstVisibleSourceOffset = 0;
            return;
        }
        var top = textView.ScrollOffset.Y;
        var line = textView.GetDocumentLineByVisualTop(top);
        var lineTop = textView.GetVisualTopByDocumentLine(line.LineNumber);
        var height = textView.GetOrConstructVisualLine(line).Height;
        var fraction = height <= 0 ? 0 : Math.Clamp((top - lineTop) / height, 0, 1);

        // NOTE: layout rounds the scroll position to whole pixels, which can leave it a fraction of a pixel above the line
        // it was scrolled to. The end of one line and the start of the next are the same place on screen.
        FirstVisibleSourceOffset = fraction > 0.99 && line.NextLine is { } next
            ? next.Offset
            : line.Offset + (int)Math.Round(fraction * line.Length);
    }
}
