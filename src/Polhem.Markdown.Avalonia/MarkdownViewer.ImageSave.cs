using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Polhem.Markdown.Avalonia.Images;

namespace Polhem.Markdown.Avalonia;

// Saving an image from the zoom overlay (polhem-dev/polhem-markdown#88).
public partial class MarkdownViewer
{
    /// <summary>Defines the <see cref="ImageSaveRequested"/> event, which bubbles.</summary>
    public static readonly RoutedEvent<MarkdownImageSaveRequestedEventArgs> ImageSaveRequestedEvent =
        RoutedEvent.Register<MarkdownViewer, MarkdownImageSaveRequestedEventArgs>(nameof(ImageSaveRequested), RoutingStrategies.Bubble);

    /// <summary>
    /// Occurs when a user asks to save the image open in the zoom overlay. A handler that marks the event handled saves
    /// the image its own way; otherwise the viewer fetches the image again through <see cref="ImageLoader"/> and saves its
    /// original bytes where the user picks in the platform's save dialog.
    /// </summary>
    public event EventHandler<MarkdownImageSaveRequestedEventArgs>? ImageSaveRequested
    {
        add => AddHandler(ImageSaveRequestedEvent, value);
        remove => RemoveHandler(ImageSaveRequestedEvent, value);
    }

    /// <summary>
    /// Where a save goes instead of the platform's save dialog, for tests: given the suggested name, the stream to write
    /// to, or <see langword="null"/> when the user cancels.
    /// </summary>
    internal Func<string, Task<Stream?>>? SaveTarget { get; set; }

    /// <summary>Raises <see cref="ImageSaveRequestedEvent"/>, and saves the image when no handler handled it.</summary>
    internal async Task SaveImageAsync(Uri source, string alternativeText, Interactive sender)
    {
        var args = new MarkdownImageSaveRequestedEventArgs(source, alternativeText);
        sender.RaiseEvent(args);
        if (args.Handled)
        {
            return;
        }
        using var bytes = await FetchOriginalAsync(source).ConfigureAwait(true);
        if (bytes is null)
        {
            return;
        }
        var name = ImageFileName.Suggest(source, bytes.GetBuffer().AsSpan(0, (int)Math.Min(bytes.Length, 512)));
        await using var target = await OpenSaveTargetAsync(name).ConfigureAwait(true);
        if (target is not null)
        {
            bytes.Position = 0;
            await bytes.CopyToAsync(target).ConfigureAwait(true);
        }
    }

    /// <summary>
    /// The image's bytes as its address serves them, through the host's loader if it has one, within the same size limit
    /// as an image shown.
    /// </summary>
    private async Task<MemoryStream?> FetchOriginalAsync(Uri source)
    {
        var loader = _imageLoader;
        try
        {
            var stream = loader is null
                ? (await ImageSources.FetchAsync(source, CancellationToken.None).ConfigureAwait(false)).Data
                : await loader(source, CancellationToken.None).ConfigureAwait(false);
            await using var owned = stream;
            return stream is null ? null : await ImageSources.ReadLimitedAsync(stream, CancellationToken.None).ConfigureAwait(false);
        }
        catch (HttpRequestException)
        {
            // The image could not be fetched again, as when the network went away after it was shown. Nothing is saved.
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private async Task<Stream?> OpenSaveTargetAsync(string name)
    {
        if (SaveTarget is { } target)
        {
            return await target(name).ConfigureAwait(true);
        }
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
        {
            return null;
        }
        var extension = Path.GetExtension(name);
        var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            SuggestedFileName = name,
            DefaultExtension = extension.Length > 0 ? extension[1..] : null,
            ShowOverwritePrompt = true,
        }).ConfigureAwait(true);
        return file is null ? null : await file.OpenWriteAsync().ConfigureAwait(true);
    }
}
