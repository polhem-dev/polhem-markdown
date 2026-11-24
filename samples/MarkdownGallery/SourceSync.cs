using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Polhem.Markdown.Avalonia;

namespace MarkdownGallery;

/// <summary>
/// Keeps an editor and a viewer in step: the viewer shows the editor's text a moment after typing stops, whichever side
/// the user scrolls brings the other to the same place in the source, and a command-click in the viewer moves the editor
/// to the source of what was clicked.
/// </summary>
/// <remarks>
/// The side under the pointer, or the editor while it has the keyboard, leads; the other follows. Following only that
/// side keeps the two from scrolling each other back and forth.
/// </remarks>
internal sealed class SourceSync
{
    private static readonly TimeSpan s_previewDelay = TimeSpan.FromMilliseconds(150);

    private readonly MarkdownEditor _editor;
    private readonly MarkdownViewer _viewer;
    private readonly ScrollViewer _viewerScroll;
    private readonly DispatcherTimer _preview;

    public SourceSync(MarkdownEditor editor, MarkdownViewer viewer, ScrollViewer viewerScroll)
    {
        _editor = editor;
        _viewer = viewer;
        _viewerScroll = viewerScroll;
        _preview = new DispatcherTimer(s_previewDelay, DispatcherPriority.Background, (_, _) => ShowPreview());

        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == MarkdownEditor.MarkdownProperty)
            {
                _preview.Stop();
                _preview.Start();
            }
            else if (e.Property == MarkdownEditor.FirstVisibleSourceOffsetProperty && EditorLeads)
            {
                ScrollViewerTo(editor.FirstVisibleSourceOffset);
            }
        };
        viewerScroll.ScrollChanged += (_, _) =>
        {
            if (!EditorLeads && ViewerSourceAtTop() is { } offset)
            {
                editor.ScrollToSourceOffset(offset);
            }
        };
        viewer.SourceRequested += (_, e) =>
        {
            editor.CaretOffset = e.SourceOffset;
            editor.ScrollToSourceOffset(e.SourceOffset);
            editor.Focus();
            e.Handled = true;
        };
    }

    private bool EditorLeads => !_viewerScroll.IsPointerOver && (_editor.IsPointerOver || _editor.IsKeyboardFocusWithin);

    private void ShowPreview()
    {
        _preview.Stop();
        _viewer.Markdown = _editor.Markdown;
    }

    private void ScrollViewerTo(int sourceOffset)
    {
        if (_viewer.GetVerticalOffset(sourceOffset) is { } y && _viewerScroll.Content is Visual content
            && _viewer.TranslatePoint(new Point(0, y), content) is { } point)
        {
            _viewerScroll.Offset = new Vector(_viewerScroll.Offset.X, point.Y);
        }
    }

    private int? ViewerSourceAtTop() =>
        _viewerScroll.Content is Visual content && content.TranslatePoint(new Point(0, _viewerScroll.Offset.Y), _viewer) is { } point
            ? _viewer.GetSourceOffset(point.Y)
            : null;
}
