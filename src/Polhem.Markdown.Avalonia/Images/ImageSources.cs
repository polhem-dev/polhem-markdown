namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// The viewer's own image loader: <c>http</c> and <c>https</c> over a shared <see cref="HttpClient"/>, <c>file</c> from the
/// file system. Which schemes reach it at all is decided by <see cref="MarkdownOptions.AllowedImageSchemes"/>.
/// </summary>
internal static class ImageSources
{
    /// <summary>The most bytes an image may have; a larger one shows its alternative text.</summary>
    public const int MaxBytes = 10 * 1024 * 1024;

    private static readonly HttpClient s_http = new() { Timeout = TimeSpan.FromSeconds(20) };

    /// <summary>The image's bytes, or <see langword="null"/> when it cannot be had.</summary>
    public static async Task<Stream?> LoadAsync(Uri source, CancellationToken cancellationToken)
    {
        switch (source.Scheme)
        {
            case "http" or "https":
                using (var response = await s_http.GetAsync(source, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > MaxBytes)
                    {
                        return null;
                    }
                    await using var body = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    return await ReadLimitedAsync(body, cancellationToken).ConfigureAwait(false);
                }
            case "file" when source.IsFile && File.Exists(source.LocalPath):
                await using (var file = File.OpenRead(source.LocalPath))
                {
                    return await ReadLimitedAsync(file, cancellationToken).ConfigureAwait(false);
                }
            default:
                return null;
        }
    }

    /// <summary>
    /// Copies at most <see cref="MaxBytes"/> from <paramref name="stream"/>; returns <see langword="null"/> when it holds
    /// more, so an endless or huge response does not fill the memory.
    /// </summary>
    public static async Task<MemoryStream?> ReadLimitedAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken).ConfigureAwait(false)) > 0)
        {
            if (buffer.Length + read > MaxBytes)
            {
                return null;
            }
            buffer.Write(chunk, 0, read);
        }
        buffer.Position = 0;
        return buffer;
    }
}
