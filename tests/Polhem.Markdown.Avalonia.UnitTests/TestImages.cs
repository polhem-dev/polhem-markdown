using SkiaSharp;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>Images and image loaders for tests, so no test reaches the network.</summary>
internal static class TestImages
{
    /// <summary>A PNG of the given size, as an image server would send it.</summary>
    public static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.OrangeRed);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>
    /// A loader that serves <paramref name="bytes"/> and records each call. The viewer loads several images at once, so
    /// the calls are recorded under a lock.
    /// </summary>
    public static Func<Uri, CancellationToken, Task<Stream?>> Serve(byte[] bytes, List<Uri>? calls = null) => (source, _) =>
    {
        if (calls is not null)
        {
            lock (calls)
            {
                calls.Add(source);
            }
        }
        return Task.FromResult<Stream?>(new MemoryStream(bytes));
    };
}
