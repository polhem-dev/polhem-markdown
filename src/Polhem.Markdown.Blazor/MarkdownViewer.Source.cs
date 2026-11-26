using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Polhem.Markdown.Blazor;

// Where the text of the source shows, for a host that keeps an editor and the viewer in step.
public partial class MarkdownViewer
{
    /// <summary>The start and length of the source of each top-level block, in the order the blocks are shown.</summary>
    private int[] _sourceRanges = [];

    /// <summary>What the module was last given, so it is told again only when something changed.</summary>
    private int[]? _watchedRanges;
    private bool _watchedScroll;
    private bool _watchedClicks;

    private ElementReference _root;
    private DotNetObjectReference<MarkdownViewerBridge>? _bridge;

    /// <summary>
    /// Gets or sets what happens when a user clicks the text with the platform's command key held (Ctrl, or Command on a
    /// Mac), to go to where it comes from in the Markdown source.
    /// </summary>
    /// <remarks>
    /// When it is set, such a click goes here and does nothing else, not even open a link; when it is not, the click
    /// does what it always does. The offset is that of the top-level block clicked, moved in proportion to how far down
    /// the block the click was, as <see cref="ScrollToSourceOffsetAsync"/> places it.
    /// </remarks>
    [Parameter]
    public EventCallback<MarkdownSourceRequestedEventArgs> OnSourceRequested { get; set; }

    /// <summary>
    /// Gets or sets what happens when scrolling brings other text to the top of the view: it receives
    /// <see cref="FirstVisibleSourceOffset"/>.
    /// </summary>
    /// <remarks>
    /// The view is the nearest element around the viewer that scrolls, or the page. The offset follows at most once a
    /// frame while the view scrolls, including when <see cref="ScrollToSourceOffsetAsync"/> scrolls it.
    /// </remarks>
    [Parameter]
    public EventCallback<int> FirstVisibleSourceOffsetChanged { get; set; }

    /// <summary>
    /// Gets the offset in <see cref="Markdown"/> of the text at the top of the view, as last reported while
    /// <see cref="FirstVisibleSourceOffsetChanged"/> is set; 0 before that.
    /// </summary>
    public int FirstVisibleSourceOffset { get; private set; }

    /// <summary>
    /// Scrolls the view around the viewer so that the text at <paramref name="sourceOffset"/> in <see cref="Markdown"/> is
    /// at its top.
    /// </summary>
    /// <remarks>
    /// The position is that of the top-level block the offset falls in, such as a paragraph, a list or a table, moved down
    /// in proportion to how far into the block's source the offset is, as the Avalonia viewer places it. Text between
    /// blocks belongs to the block before it. Rendered statically, or with nothing to show, it does nothing.
    /// </remarks>
    /// <param name="sourceOffset">An offset in <see cref="Markdown"/>, in UTF-16 code units.</param>
    /// <returns>A task that completes when the view has scrolled.</returns>
    public async Task ScrollToSourceOffsetAsync(int sourceOffset)
    {
        if (!RendererInfo.IsInteractive || _sourceRanges.Length == 0 || await WatchSourceAsync(force: true) is not { } module)
        {
            return;
        }
        try
        {
            await module.InvokeVoidAsync("scrollToSource", _root, sourceOffset);
        }
        catch (JSException)
        {
            // The viewer left the page.
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone.
        }
        catch (TaskCanceledException)
        {
            // The call timed out on a slow connection.
        }
    }

    internal void SetRootElement(ElementReference element) => _root = element;

    /// <summary>The source ranges the module is given, for the tests.</summary>
    internal IReadOnlyList<int> SourceRanges => _sourceRanges;

    private void KeepSourceRanges() =>
        _sourceRanges = [.. _document.Blocks.SelectMany(block => new[] { block.Range.Start, block.Range.Length })];

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if ((OnSourceRequested.HasDelegate || FirstVisibleSourceOffsetChanged.HasDelegate) && RendererInfo.IsInteractive)
        {
            await WatchSourceAsync(force: false);
        }
        else if (_watchedRanges is not null)
        {
            await StopWatchingSourceAsync();
        }
        if (_zoomPending)
        {
            await OpenZoomAsync();
        }
    }

    /// <summary>
    /// Gives the module the source ranges and what to report, when either changed since it was last given them or
    /// <paramref name="force"/> is set, and returns the module, or <see langword="null"/> when it is not available.
    /// </summary>
    private async Task<IJSObjectReference?> WatchSourceAsync(bool force)
    {
        var scroll = FirstVisibleSourceOffsetChanged.HasDelegate;
        var clicks = OnSourceRequested.HasDelegate;
        if (!force && ReferenceEquals(_watchedRanges, _sourceRanges) && _watchedScroll == scroll && _watchedClicks == clicks)
        {
            return null;
        }
        try
        {
            _module ??= JS.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
            var module = await _module;
            _bridge ??= DotNetObjectReference.Create(new MarkdownViewerBridge(this));
            await module.InvokeVoidAsync("watchSource", _root, _bridge, _sourceRanges, scroll, clicks);
            (_watchedRanges, _watchedScroll, _watchedClicks) = (_sourceRanges, scroll, clicks);
            return module;
        }
        catch (JSException)
        {
            ForgetFailedImport();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone.
        }
        catch (TaskCanceledException)
        {
            ForgetFailedImport();
        }
        return null;
    }

    private async Task StopWatchingSourceAsync()
    {
        var watched = _watchedRanges is not null;
        _watchedRanges = null;
        if (watched && _module is { IsCompletedSuccessfully: true } module)
        {
            try
            {
                await (await module).InvokeVoidAsync("unwatchSource", _root);
            }
            catch (JSException)
            {
                // The viewer already left the page.
            }
            catch (JSDisconnectedException)
            {
                // The circuit is gone.
            }
            catch (TaskCanceledException)
            {
                // The page did not answer in time.
            }
        }
        _bridge?.Dispose();
        _bridge = null;
    }

    /// <summary>
    /// Takes the offset of the text now at the top of the view, from the module. Under interactive server rendering the
    /// value comes from the browser, so it is kept within the text.
    /// </summary>
    internal async Task OnFirstVisibleChangedAsync(int sourceOffset)
    {
        var offset = Math.Clamp(sourceOffset, 0, Markdown?.Length ?? 0);
        if (offset == FirstVisibleSourceOffset)
        {
            return;
        }
        FirstVisibleSourceOffset = offset;
        await FirstVisibleSourceOffsetChanged.InvokeAsync(offset);
    }

    /// <summary>Takes a click with the command key held, from the module.</summary>
    internal Task OnSourceClickedAsync(int sourceOffset) =>
        OnSourceRequested.InvokeAsync(new MarkdownSourceRequestedEventArgs(Math.Clamp(sourceOffset, 0, Markdown?.Length ?? 0)));
}
