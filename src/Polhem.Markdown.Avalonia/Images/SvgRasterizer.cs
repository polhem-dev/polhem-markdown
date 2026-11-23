using System.Xml;
using Avalonia;
using Avalonia.Media.Imaging;
using SkiaSharp;
using Svg;
using Svg.Skia;

namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// Draws an SVG into a bitmap with Svg.Skia, for the viewer's image runs. The SVG reaches nothing outside itself: DTD
/// processing is prohibited, so no entity is expanded or fetched, and external resources (images, <c>use</c> targets,
/// filters, style sheets) are disabled for the document. Scripts do not run; Svg.Skia runs none unless given a runtime.
/// </summary>
internal static class SvgRasterizer
{
    /// <summary>The bitmap is drawn at twice the SVG's size, so it stays sharp on high-density screens.</summary>
    private const float Density = 2;

    /// <summary>The largest side of the bitmap in pixels; a larger SVG is drawn smaller.</summary>
    private const int MaxPixels = 4096;

    /// <summary>Whether the bytes look like SVG rather than a raster image.</summary>
    public static bool IsSvg(ReadOnlySpan<byte> data)
    {
        var start = data[..Math.Min(data.Length, 1024)];
        if (start.StartsWith("﻿"u8))
        {
            start = start[3..];
        }
        start = start.TrimStart(" \t\r\n"u8);
        return start.StartsWith("<svg"u8) || ((start.StartsWith("<?xml"u8) || start.StartsWith("<!--"u8)) && start.IndexOf("<svg"u8) >= 0);
    }

    /// <summary>The SVG as a bitmap and its size, or <see langword="null"/> when it cannot be drawn.</summary>
    public static LoadedImage? Rasterize(Stream data)
    {
        SvgDocument document;
        using (var reader = XmlReader.Create(data, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
        {
            document = SvgDocument.Open<SvgDocument>(reader);
        }
        document.LoadOptions = new SvgDocumentLoadOptions { ExternalResources = SvgExternalResourcePolicy.Disabled };

        using var svg = new SKSvg();
        if (svg.FromSvgDocument(document) is not { } picture)
        {
            return null;
        }
        var bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return null;
        }

        var scale = Math.Min(Density, MaxPixels / Math.Max(bounds.Width, bounds.Height));
        var width = Math.Max(1, (int)Math.Ceiling(bounds.Width * scale));
        var height = Math.Max(1, (int)Math.Ceiling(bounds.Height * scale));
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
        return new LoadedImage(new Bitmap(stream), new Size(bounds.Width, bounds.Height));
    }
}
