using Microsoft.JSInterop;

namespace Polhem.Markdown.Blazor;

/// <summary>
/// What the script of a <see cref="MarkdownViewer"/> calls, kept off the viewer so the calls it needs to be public for
/// are not part of the viewer's API.
/// </summary>
internal sealed class MarkdownViewerBridge(MarkdownViewer viewer)
{
    /// <summary>Takes the source offset of the text now at the top of the view.</summary>
    [JSInvokable]
    public Task FirstVisibleChanged(int sourceOffset) => viewer.OnFirstVisibleChangedAsync(sourceOffset);

    /// <summary>Takes a click with the command key held, at a source offset.</summary>
    [JSInvokable]
    public Task SourceRequested(int sourceOffset) => viewer.OnSourceClickedAsync(sourceOffset);

    /// <summary>Takes the closing of the dialog that shows an image at full size.</summary>
    [JSInvokable]
    public void ZoomClosed() => viewer.OnZoomClosed();
}
