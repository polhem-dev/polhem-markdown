using Polhem.Markdown.Avalonia.Images;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>The most bytes the decoded images of one viewer may take; images beyond it show their alternative text.</summary>
    internal const long ImageBudgetBytes = 256L * 1024 * 1024;

    private readonly Dictionary<Uri, Task<LoadedImage?>> _images = [];

    private readonly ImageBudget _imageBudget = new(ImageBudgetBytes);

    private Func<Uri, CancellationToken, Task<Stream?>>? _imageLoader;

    /// <summary>
    /// Gets or sets what loads an image: it returns the image's bytes, or <see langword="null"/> when the image cannot be
    /// had. <see langword="null"/> uses the viewer's own loader, which fetches <c>http</c> and <c>https</c> images and
    /// reads <c>file</c> ones.
    /// </summary>
    /// <remarks>
    /// Set it to add authentication, caching or a proxy. It is called only for sources whose scheme is in
    /// <see cref="MarkdownOptions.AllowedImageSchemes"/>, on a background thread, once per source while the viewer
    /// remembers it; an image larger than 10 MB, one that cannot be decoded, and one past the viewer's budget for decoded
    /// images show their alternative text. SVG images are drawn without fetching anything they refer to.
    /// </remarks>
    public Func<Uri, CancellationToken, Task<Stream?>>? ImageLoader
    {
        get => _imageLoader;
        set
        {
            _imageLoader = value;
            foreach (var loading in _images.Values)
            {
                ReleaseWhenLoaded(loading);
            }
            _images.Clear();
            Update(fullRebuild: true);
        }
    }

    /// <summary>The bytes the decoded images take now, for tests.</summary>
    internal long ImageBytesInUse => _imageBudget.Used;

    /// <summary>
    /// The image at <paramref name="source"/>, loaded once and remembered while the text shows it, so a reply that is
    /// still arriving does not fetch its images again on every change.
    /// </summary>
    internal Task<LoadedImage?> LoadImage(Uri source)
    {
        if (_images.TryGetValue(source, out var loading))
        {
            return loading;
        }
        var loader = _imageLoader ?? ImageSources.LoadAsync;
        loading = Task.Run(async () =>
        {
            await using var stream = await loader(source, CancellationToken.None).ConfigureAwait(false);
            if (stream is null)
            {
                return null;
            }
            using var data = await ImageSources.ReadLimitedAsync(stream, CancellationToken.None).ConfigureAwait(false);
            return data is null ? null : ImageDecoder.Decode(data, _imageBudget);
        });
        _images[source] = loading;
        return loading;
    }

    /// <summary>
    /// Forgets the images the text no longer shows, so the cache, and the budget it holds, follow the text: a reply that
    /// grows keeps its images, and a new text starts from an empty budget.
    /// </summary>
    private void ForgetImagesNotIn(DocumentNode document)
    {
        if (_images.Count == 0)
        {
            return;
        }
        var shown = document.Blocks.SelectMany(NodeTree.SelfAndDescendants).OfType<ImageNode>().Select(image => image.Source).ToHashSet();
        foreach (var source in _images.Keys.Where(source => !shown.Contains(source)).ToList())
        {
            ReleaseWhenLoaded(_images[source]);
            _images.Remove(source);
        }
    }

    /// <summary>Gives a forgotten image's bytes back to the budget, now or when its load ends.</summary>
    private void ReleaseWhenLoaded(Task<LoadedImage?> loading)
    {
        if (loading.IsCompleted)
        {
            if (loading.IsCompletedSuccessfully && loading.Result is { } loaded)
            {
                _imageBudget.Release(loaded.Bytes);
            }
            return;
        }
        _ = loading.ContinueWith(
            finished => ReleaseWhenLoaded(finished),
            CancellationToken.None,
            TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
