using System.Xml;
using Avalonia.Media.Imaging;
using SkiaSharp;

namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// Turns an image's bytes into something the viewer can show: SVG through <see cref="SvgRasterizer"/>, anything else as
/// a bitmap.
/// </summary>
/// <remarks>
/// NOTE: a raster image's size is read from its header before it is decoded. A few kilobytes of PNG can declare a
/// picture that takes gigabytes once decoded, so one larger than <see cref="MaxPixels"/>, or one the viewer's budget
/// cannot hold, is not decoded at all.
/// </remarks>
internal static class ImageDecoder
{
    /// <summary>The most pixels a raster image may have: 4096 by 4096.</summary>
    public const long MaxPixels = 4096L * 4096;

    /// <summary>
    /// The decoded image, or <see langword="null"/> when the bytes are not an image the viewer can show, are an SVG that
    /// is not <paramref name="svgTrusted"/>, or <paramref name="budget"/> cannot hold the bytes its pixels need.
    /// </summary>
    public static LoadedImage? Decode(MemoryStream data, ImageBudget budget, bool svgTrusted)
    {
        if (SvgRasterizer.IsSvg(data.GetBuffer().AsSpan(0, (int)data.Length)))
        {
            return svgTrusted ? RasterizeSvg(data, budget) : null;
        }
        return DecodeBitmap(data, budget);
    }

    private static LoadedImage? DecodeBitmap(MemoryStream data, ImageBudget budget)
    {
        long bytes;
        using (var codec = SKCodec.Create(new MemoryStream(data.GetBuffer(), 0, (int)data.Length, writable: false)))
        {
            if (codec is null)
            {
                return null;
            }
            var pixels = (long)codec.Info.Width * codec.Info.Height;
            if (pixels <= 0 || pixels > MaxPixels)
            {
                return null;
            }
            bytes = pixels * 4;
        }
        if (!budget.TryReserve(bytes))
        {
            return null;
        }
        try
        {
            data.Position = 0;
            var bitmap = new Bitmap(data);
            return new LoadedImage(bitmap, bitmap.Size, bytes);
        }
        catch (ArgumentException)
        {
            // Skia refuses bytes it cannot decode; the image then shows its alternative text.
            budget.Release(bytes);
            return null;
        }
        catch (InvalidOperationException)
        {
            budget.Release(bytes);
            return null;
        }
    }

    /// <summary>The stack of the thread an SVG is drawn on: far more than a thread pool thread has.</summary>
    private const int SvgStackBytes = 64 * 1024 * 1024;

    /// <summary>
    /// Draws the SVG on a thread of its own with a large stack. Svg.Skia recurses through nested elements and the
    /// references between them, and a stack overflow ends the process, which nothing can catch. Only SVG from trusted
    /// hosts gets here (ADR-001); the large stack keeps a file from one of them that recurses too far from taking the
    /// application down.
    /// </summary>
    private static LoadedImage? RasterizeSvg(MemoryStream data, ImageBudget budget)
    {
        LoadedImage? result = null;
        var thread = new Thread(() => result = RasterizeOrNull(data, budget), SvgStackBytes)
        {
            IsBackground = true,
            Name = "Polhem.Markdown SVG",
        };
        thread.Start();
        thread.Join();
        return result;
    }

    private static LoadedImage? RasterizeOrNull(MemoryStream data, ImageBudget budget)
    {
        try
        {
            return SvgRasterizer.Rasterize(data, budget);
        }
        catch (XmlException)
        {
            // Not well-formed, or it declares a DTD, which is refused. The image shows its alternative text.
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
