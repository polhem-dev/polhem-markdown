using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

public partial class MarkdownViewer
{
    private ImageNode? _zoomed;
    private ElementReference _zoomDialog;
    private bool _zoomPending;

    /// <summary>
    /// Gets or sets whether a click on an image, or Enter on it, opens it at full size over the page, where it can be
    /// zoomed and panned. Off by default. An image inside a link opens the link instead.
    /// </summary>
    [Parameter]
    public bool ZoomImages { get; set; }

    /// <summary>
    /// Gets or sets what happens when a user opens an image while <see cref="ZoomImages"/> is on. When it is set, the
    /// host shows the image its own way and the viewer opens nothing.
    /// </summary>
    [Parameter]
    public EventCallback<MarkdownImageActivatedEventArgs> OnImageActivated { get; set; }

    /// <summary>
    /// Gets or sets what happens when a user asks to save the image open at full size. When it is set, the host saves
    /// the image its own way; when it is not, the browser fetches the image and downloads it, or, when the image's host
    /// does not let the page read it, opens it in a new tab to be saved from there.
    /// </summary>
    [Parameter]
    public EventCallback<MarkdownImageSaveRequestedEventArgs> OnImageSaveRequested { get; set; }

    /// <summary>The accessible name and tooltip of the button that closes an image opened at full size.</summary>
    internal const string ZoomCloseLabel = "Close";

    /// <summary>The accessible name and tooltip of the button that saves an image opened at full size.</summary>
    internal const string ZoomSaveLabel = "Save image";

    internal async Task SaveZoomedImageAsync(ImageNode image)
    {
        if (OnImageSaveRequested.HasDelegate)
        {
            await OnImageSaveRequested.InvokeAsync(new MarkdownImageSaveRequestedEventArgs(image.Source, image.AlternativeText));
            return;
        }
        try
        {
            _module ??= JS.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
            var module = await _module;
            await module.InvokeVoidAsync("saveImage", _zoomDialog, ImageFileName.Suggest(image.Source, []));
        }
        catch (JSException)
        {
            // The dialog closed before the script ran, or the module could not load. Nothing is saved.
            ForgetFailedImport();
        }
        catch (TaskCanceledException)
        {
            ForgetFailedImport();
        }
    }

    /// <summary>The image open at full size, for tests.</summary>
    internal ImageNode? ZoomedImage => _zoomed;

    internal void SetZoomDialog(ElementReference element) => _zoomDialog = element;

    internal async Task ActivateImageAsync(ImageNode image)
    {
        if (OnImageActivated.HasDelegate)
        {
            await OnImageActivated.InvokeAsync(new MarkdownImageActivatedEventArgs(image.Source, image.AlternativeText));
            return;
        }
        if (_zoomed is not null)
        {
            return;
        }
        _zoomed = image;
        _zoomPending = true;
        StateHasChanged();
    }

    /// <summary>Has the script open the dialog the last render wrote.</summary>
    private async Task OpenZoomAsync()
    {
        _zoomPending = false;
        try
        {
            _module ??= JS.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
            var module = await _module;
            _bridge ??= DotNetObjectReference.Create(new MarkdownViewerBridge(this));
            await module.InvokeVoidAsync("openZoom", _zoomDialog, _bridge);
            return;
        }
        catch (JSException)
        {
            ForgetFailedImport();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone.
            return;
        }
        catch (TaskCanceledException)
        {
            ForgetFailedImport();
        }

        // Without the script the dialog cannot open, so it is not left in the page.
        OnZoomClosed();
    }

    /// <summary>Takes the dialog out of the page once the script reports it closed.</summary>
    internal void OnZoomClosed()
    {
        _zoomed = null;
        _zoomPending = false;
        StateHasChanged();
    }
}
