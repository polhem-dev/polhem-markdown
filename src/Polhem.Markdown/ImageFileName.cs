namespace Polhem.Markdown;

/// <summary>
/// The name an image is saved under from a viewer's zoom overlay: the last segment of its address, with characters no
/// file system takes replaced, and an extension for its format when the address has none (polhem-dev/polhem-markdown#88).
/// </summary>
internal static class ImageFileName
{
    private static readonly string[] s_knownExtensions = [".png", ".jpg", ".jpeg", ".gif", ".webp", ".svg", ".bmp", ".ico", ".avif"];

    /// <summary>
    /// The suggested name for the image at <paramref name="source"/>, whose bytes start with <paramref name="start"/>,
    /// from which its format is told when the address does not tell it.
    /// </summary>
    public static string Suggest(Uri source, ReadOnlySpan<byte> start)
    {
        var name = source.IsAbsoluteUri && source.Scheme is "http" or "https" or "file"
            ? Uri.UnescapeDataString(source.AbsolutePath.TrimEnd('/').Split('/')[^1])
            : "";
        name = string.Concat(name.Select(c => char.IsControl(c) || c is '\\' or '/' or ':' or '*' or '?' or '"' or '<' or '>' or '|' ? '_' : c))
            .Trim(' ', '.');
        if (name.Length == 0)
        {
            name = "image";
        }
        if (name.Length > MaxLength)
        {
            name = name[..MaxLength];
        }
        var extension = Path.GetExtension(name);
        if (!s_knownExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) && ExtensionOf(start) is { } sniffed)
        {
            name += sniffed;
        }
        return name;
    }

    private const int MaxLength = 120;

    /// <summary>The extension of the format the bytes start as, or <see langword="null"/>.</summary>
    public static string? ExtensionOf(ReadOnlySpan<byte> start)
    {
        if (start.StartsWith((ReadOnlySpan<byte>)[0x89, (byte)'P', (byte)'N', (byte)'G']))
        {
            return ".png";
        }
        if (start.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
        {
            return ".jpg";
        }
        if (start.StartsWith("GIF8"u8))
        {
            return ".gif";
        }
        if (start.Length >= 12 && start.StartsWith("RIFF"u8) && start[8..12].SequenceEqual("WEBP"u8))
        {
            return ".webp";
        }
        if (start.StartsWith("BM"u8))
        {
            return ".bmp";
        }
        var text = System.Text.Encoding.UTF8.GetString(start[..Math.Min(start.Length, 512)]).TrimStart('﻿', ' ', '\t', '\r', '\n');
        return text.StartsWith("<svg", StringComparison.OrdinalIgnoreCase)
            || (text.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase) && text.Contains("<svg", StringComparison.OrdinalIgnoreCase))
            ? ".svg"
            : null;
    }
}
