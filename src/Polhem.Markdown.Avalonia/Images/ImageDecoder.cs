using System.Xml;
using Avalonia.Media.Imaging;

namespace Polhem.Markdown.Avalonia.Images;

/// <summary>Turns an image's bytes into something the viewer can show: SVG through <see cref="SvgRasterizer"/>, anything else as a bitmap.</summary>
internal static class ImageDecoder
{
    /// <summary>The decoded image, or <see langword="null"/> when the bytes are not an image the viewer can show.</summary>
    public static LoadedImage? Decode(MemoryStream data)
    {
        if (SvgRasterizer.IsSvg(data.GetBuffer().AsSpan(0, (int)data.Length)))
        {
            return RasterizeSvg(data);
        }
        return DecodeBitmap(data);
    }

    private static LoadedImage? DecodeBitmap(MemoryStream data)
    {
        try
        {
            var bitmap = new Bitmap(data);
            return new LoadedImage(bitmap, bitmap.Size);
        }
        catch (ArgumentException)
        {
            // Skia refuses bytes it cannot decode; the image then shows its alternative text.
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private static LoadedImage? RasterizeSvg(MemoryStream data)
    {
        try
        {
            return SvgRasterizer.Rasterize(data);
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
