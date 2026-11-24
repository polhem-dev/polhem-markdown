using Polhem.Markdown.Avalonia.Images;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>The most bytes the decoded images of one viewer may take; images beyond it show their alternative text.</summary>
    internal const long ImageBudgetBytes = 256L * 1024 * 1024;

    /// <summary>How many images one viewer loads and decodes at the same time.</summary>
    internal const int MaxConcurrentImageLoads = 4;

    private readonly Dictionary<Uri, Task<LoadedImage?>> _images = [];

    private readonly ImageBudget _imageBudget = new(ImageBudgetBytes);
    private readonly SemaphoreSlim _imageSlots = new(MaxConcurrentImageLoads);

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
    /// images show their alternative text. An SVG image is drawn only from a host in <see cref="TrustedSvgHosts"/>, and
    /// without fetching anything it refers to.
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

    /// <summary>
    /// Gets the hosts whose <c>https</c> SVG images the viewer draws, compared without regard to case. The default is a
    /// few badge services, such as <c>img.shields.io</c>, <c>badgen.net</c> and <c>codecov.io</c>; GitHub's workflow
    /// badges are drawn too.
    /// </summary>
    /// <remarks>
    /// An SVG from any other host shows its alternative text. Drawing an SVG means running a complex parser on what the
    /// server sends, and a small hostile file can exhaust the processor and memory, which no SVG renderer for .NET
    /// bounds. The default hosts generate their badges themselves and serve nothing a user uploaded. Add a host only when
    /// it serves no file a user can upload (a code host's raw files, a build's artifacts) and its SVG is trusted as much
    /// as code. With the viewer's own loader, the address an image finally came from, after any redirect, must be
    /// trusted too. A change applies to images loaded afterwards.
    /// </remarks>
    public ISet<string> TrustedSvgHosts { get; } = new HashSet<string>(SvgTrust.DefaultHosts, StringComparer.OrdinalIgnoreCase);

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
        var loader = _imageLoader;
        var trustedHosts = new HashSet<string>(TrustedSvgHosts, StringComparer.OrdinalIgnoreCase);
        loading = Task.Run(async () =>
        {
            // NOTE: loads wait for one another past a few at a time, so a reply with many image addresses costs no more
            // memory and processor time at once than a few images do.
            await _imageSlots.WaitAsync().ConfigureAwait(false);
            try
            {
                // NOTE: an SVG is drawn only when trusted (ADR-001). With the viewer's own loader the address it finally
                // came from counts too, so a trusted host cannot redirect to an untrusted one; a host's loader answers for
                // where it fetches.
                Stream? stream;
                var trusted = SvgTrust.IsTrusted(source, trustedHosts);
                if (loader is null)
                {
                    var (data, final) = await ImageSources.FetchAsync(source, CancellationToken.None).ConfigureAwait(false);
                    stream = data;
                    trusted = trusted && SvgTrust.IsTrusted(final, trustedHosts);
                }
                else
                {
                    stream = await loader(source, CancellationToken.None).ConfigureAwait(false);
                }
                await using var owned = stream;
                if (stream is null)
                {
                    return null;
                }
                using var bytes = await ImageSources.ReadLimitedAsync(stream, CancellationToken.None).ConfigureAwait(false);
                return bytes is null ? null : ImageDecoder.Decode(bytes, _imageBudget, svgTrusted: trusted);
            }
            finally
            {
                _imageSlots.Release();
            }
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
