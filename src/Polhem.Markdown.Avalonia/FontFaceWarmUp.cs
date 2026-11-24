using System.Collections.Concurrent;
using Avalonia.Media;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Loads the heavier faces of an installed font family ahead of use, so bold text in that family finds its nearest face.
/// </summary>
/// <remarks>
/// NOTE: when a family has no face of exactly the weight asked for, such as PingFang TC, whose heaviest face is
/// semibold, Avalonia picks the nearest weight among the faces of that family it has already loaded. If only the regular
/// face has been loaded, bold text in that family comes out regular. Loading the heavier faces first gives it the
/// semibold one to pick. See <see cref="MarkdownViewer.CjkFontFamily"/>.
/// </remarks>
internal static class FontFaceWarmUp
{
    private static readonly ConcurrentDictionary<string, byte> s_loadedFamilies = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Loads the faces heavier than regular of each installed family in <paramref name="family"/>, once per family.</summary>
    public static void LoadHeavierFaces(FontFamily family)
    {
        var fontManager = FontManager.Current;
        foreach (var name in family.FamilyNames)
        {
            if (!s_loadedFamilies.TryAdd(name, 0) || !fontManager.SystemFonts.TryGetFamilyTypefaces(name, out var faces))
            {
                continue;
            }
            foreach (var face in faces.Where(face => face.Weight > FontWeight.Normal))
            {
                fontManager.TryGetGlyphTypeface(face, out _);
            }
        }
    }
}
