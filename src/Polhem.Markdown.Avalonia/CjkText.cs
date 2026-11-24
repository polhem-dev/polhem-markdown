using System.Text;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Splits text into stretches of Chinese, Japanese and Korean characters and stretches of other characters, so the
/// viewer can draw the former in <see cref="MarkdownViewer.CjkFontFamily"/>.
/// </summary>
internal static class CjkText
{
    /// <summary>
    /// Returns the stretches of <paramref name="text"/> in order, each with whether it is CJK. Together they cover the
    /// whole text, and a surrogate pair is never split.
    /// </summary>
    public static IReadOnlyList<(int Start, int Length, bool IsCjk)> Split(string text)
    {
        var stretches = new List<(int Start, int Length, bool IsCjk)>();
        var start = 0;
        var startIsCjk = false;
        var index = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            var isCjk = IsCjk(rune);
            if (index > 0 && isCjk != startIsCjk)
            {
                stretches.Add((start, index - start, startIsCjk));
                start = index;
            }
            startIsCjk = isCjk;
            index += rune.Utf16SequenceLength;
        }
        if (index > start)
        {
            stretches.Add((start, index - start, startIsCjk));
        }
        return stretches;
    }

    /// <summary>
    /// Whether a character belongs to a CJK block: Han ideographs and their radicals and strokes, kana, Bopomofo, Hangul,
    /// CJK symbols and punctuation, and the full-width forms.
    /// </summary>
    internal static bool IsCjk(Rune rune) => rune.Value switch
    {
        >= 0x1100 and <= 0x11FF => true,   // Hangul Jamo
        >= 0x2E80 and <= 0x2FFF => true,   // CJK and Kangxi radicals, ideographic description characters
        >= 0x3000 and <= 0x4DBF => true,   // CJK punctuation, kana, Bopomofo, Hangul compatibility Jamo, extension A
        >= 0x4E00 and <= 0x9FFF => true,   // CJK unified ideographs
        >= 0xA960 and <= 0xA97F => true,   // Hangul Jamo extended A
        >= 0xAC00 and <= 0xD7FF => true,   // Hangul syllables, Hangul Jamo extended B
        >= 0xF900 and <= 0xFAFF => true,   // CJK compatibility ideographs
        >= 0xFE30 and <= 0xFE4F => true,   // CJK compatibility forms
        >= 0xFF00 and <= 0xFFEF => true,   // half-width and full-width forms
        >= 0x20000 and <= 0x3FFFF => true, // supplementary and tertiary ideographic planes
        _ => false,
    };
}
