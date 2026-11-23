using System.Xml;
using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Svg;
using Svg.Model;
using Svg.Skia;

namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// Draws an SVG into a bitmap with Svg.Skia, for the viewer's image runs.
/// </summary>
/// <remarks>
/// The SVG comes from any server an image points at, so it is checked before Svg.Skia reads it and drawn with limits:
/// <list type="bullet">
/// <item>it reaches nothing outside itself: DTD processing is prohibited, so no entity is expanded or fetched, and
/// external resources (images, <c>use</c> targets, filters, style sheets) are disabled for the document;</item>
/// <item>scripts do not run: Svg.Skia runs none unless given a runtime;</item>
/// <item>its nesting, element count and <c>use</c> elements are limited by <see cref="IsWithinLimits"/>, because Svg.Skia
/// recurses into nested elements (a few thousand levels overflow the stack, which ends the process) and copies what each
/// <c>use</c> refers to (a <c>use</c> of something holding <c>use</c> elements doubles per level);</item>
/// <item>filters are not drawn, since a blur over a large area takes seconds; and the bitmap is at most
/// <see cref="MaxPixels"/> a side.</item>
/// </list>
/// </remarks>
internal static class SvgRasterizer
{
    /// <summary>The largest side of the bitmap in pixels; a larger SVG is drawn smaller.</summary>
    public const int MaxPixels = 2048;

    /// <summary>The bitmap is drawn at twice the SVG's size, so it stays sharp on high-density screens.</summary>
    private const float Density = 2;

    private const int MaxDepth = 64;
    private const int MaxElements = 2000;
    private const int MaxUses = 64;

    private static readonly byte[] s_byteOrderMark = [0xEF, 0xBB, 0xBF];

    /// <summary>Whether the bytes look like SVG rather than a raster image.</summary>
    public static bool IsSvg(ReadOnlySpan<byte> data)
    {
        var start = data[..Math.Min(data.Length, 1024)];
        if (start.StartsWith(s_byteOrderMark))
        {
            start = start[s_byteOrderMark.Length..];
        }
        start = start.TrimStart(" \t\r\n"u8);
        return start.StartsWith("<svg"u8) || ((start.StartsWith("<?xml"u8) || start.StartsWith("<!--"u8)) && start.IndexOf("<svg"u8) >= 0);
    }

    /// <summary>
    /// The SVG as a bitmap, or <see langword="null"/> when it cannot be drawn or <paramref name="budget"/> cannot hold the
    /// bytes the bitmap needs, which are reserved before the bitmap is made.
    /// </summary>
    public static LoadedImage? Rasterize(MemoryStream data, ImageBudget budget)
    {
        if (!IsWithinLimits(data))
        {
            return null;
        }
        data.Position = 0;
        SvgDocument document;
        using (var reader = XmlReader.Create(data, ReaderSettings()))
        {
            document = SvgDocument.Open<SvgDocument>(reader);
        }
        document.LoadOptions = new SvgDocumentLoadOptions { ExternalResources = SvgExternalResourcePolicy.Disabled };

        using var svg = new SKSvg { IgnoreAttributes = DrawAttributes.Filter };
        if (svg.FromSvgDocument(document) is not { } picture)
        {
            return null;
        }
        var bounds = picture.CullRect;
        if (!(bounds.Width > 0 && bounds.Height > 0 && float.IsFinite(bounds.Width) && float.IsFinite(bounds.Height)))
        {
            return null;
        }
        var scale = Math.Min(Density, MaxPixels / Math.Max(bounds.Width, bounds.Height));
        var width = PixelSize(bounds.Width, scale);
        var height = PixelSize(bounds.Height, scale);
        var bytes = (long)width * height * 4;
        if (!budget.TryReserve(bytes))
        {
            return null;
        }

        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.Scale(scale);
            canvas.Translate(-bounds.Left, -bounds.Top);
            canvas.DrawPicture(picture);
        }
        using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = png.AsStream();
        return new LoadedImage(new Bitmap(stream), new Size(bounds.Width, bounds.Height), bytes);
    }

    private static int PixelSize(float length, float scale) => Math.Clamp((int)Math.Ceiling(length * scale), 1, MaxPixels);

    /// <summary>
    /// Reads the SVG once, without building anything, and checks its nesting, element count and <c>use</c> elements:
    /// no <c>use</c> may refer to an element that itself holds a <c>use</c>.
    /// </summary>
    private static bool IsWithinLimits(MemoryStream data)
    {
        data.Position = 0;
        var elements = 0;
        var uses = new List<string>();
        var holdsUse = new HashSet<string>(StringComparer.Ordinal);
        var open = new List<string?>();
        using var reader = XmlReader.Create(data, ReaderSettings());
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }
            if (++elements > MaxElements || reader.Depth >= MaxDepth)
            {
                return false;
            }
            while (open.Count > reader.Depth)
            {
                open.RemoveAt(open.Count - 1);
            }
            if (reader.LocalName == "use")
            {
                if (uses.Count >= MaxUses)
                {
                    return false;
                }
                var target = reader.GetAttribute("href") ?? reader.GetAttribute("href", "http://www.w3.org/1999/xlink");
                if (target is ['#', ..])
                {
                    uses.Add(target[1..]);
                }
                foreach (var ancestor in open)
                {
                    if (ancestor is not null)
                    {
                        holdsUse.Add(ancestor);
                    }
                }
            }
            if (!reader.IsEmptyElement)
            {
                open.Add(reader.GetAttribute("id"));
            }
        }
        return !uses.Exists(holdsUse.Contains);
    }

    private static XmlReaderSettings ReaderSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        MaxCharactersInDocument = ImageSources.MaxBytes,
    };
}
