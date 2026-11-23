using System.Globalization;
using System.Text;

namespace Polhem.Markdown.Diagrams;

/// <summary>
/// Estimates the size of diagram text from its characters instead of measuring it.
/// </summary>
/// <remarks>
/// NOTE: a Blazor component that renders on the server cannot measure text in the browser, and two viewers that
/// measured would each compute a different layout. An estimate gives every viewer the same coordinates; boxes are sized
/// with padding, so a font a little wider or narrower than the estimate still fits.
/// </remarks>
internal static class TextMetrics
{
    /// <summary>The height of one line of text.</summary>
    public const double LineHeight = 20;

    /// <summary>Splits a label into its lines at Mermaid's <c>&lt;br&gt;</c> line breaks.</summary>
    public static IReadOnlyList<string> Lines(string label)
    {
        var lines = new List<string>();
        var current = new StringBuilder();
        var index = 0;
        while (index < label.Length)
        {
            if (label[index] == '<' && LineBreakLength(label, index) is var length and > 0)
            {
                lines.Add(current.ToString().Trim());
                current.Clear();
                index += length;
                continue;
            }
            current.Append(label[index]);
            index++;
        }
        lines.Add(current.ToString().Trim());
        return lines;
    }

    /// <summary>The estimated width of the widest line.</summary>
    public static double Width(IReadOnlyList<string> lines) => lines.Count == 0 ? 0 : lines.Max(Width);

    /// <summary>The height of <paramref name="lines"/> stacked.</summary>
    public static double Height(IReadOnlyList<string> lines) => lines.Count * LineHeight;

    /// <summary>The estimated width of one line.</summary>
    public static double Width(string line)
    {
        var em = 0.0;
        var enumerator = StringInfo.GetTextElementEnumerator(line);
        while (enumerator.MoveNext())
        {
            em += Em(enumerator.GetTextElement());
        }
        return em * DiagramDrawing.FontSize;
    }

    private static double Em(string element)
    {
        var rune = Rune.GetRuneAt(element, 0);
        var value = rune.Value;
        if (value >= 0x1100 && IsWide(value))
        {
            return 1.0;
        }
        return rune.IsAscii ? Ascii((char)value) : 0.62;
    }

    private static double Ascii(char c) => c switch
    {
        ' ' => 0.3,
        'i' or 'j' or 'l' or '.' or ',' or ':' or ';' or '\'' or '|' or '!' or 'I' => 0.3,
        'f' or 't' or 'r' or '(' or ')' or '[' or ']' or '-' or '"' => 0.4,
        'm' or 'w' or 'M' or 'W' or '@' => 0.9,
        >= 'A' and <= 'Z' => 0.7,
        >= '0' and <= '9' => 0.58,
        _ => 0.56,
    };

    /// <summary>Whether a code point is drawn a full em wide: CJK, Hangul, fullwidth forms and emoji.</summary>
    private static bool IsWide(int value) =>
        value is <= 0x115F
            or (>= 0x2E80 and <= 0xA4CF)
            or (>= 0xAC00 and <= 0xD7A3)
            or (>= 0xF900 and <= 0xFAFF)
            or (>= 0xFE30 and <= 0xFE4F)
            or (>= 0xFF00 and <= 0xFF60)
            or (>= 0xFFE0 and <= 0xFFE6)
            or >= 0x1F300;

    private static int LineBreakLength(string text, int index)
    {
        foreach (var form in s_lineBreaks)
        {
            if (string.Compare(text, index, form, 0, form.Length, StringComparison.OrdinalIgnoreCase) == 0)
            {
                return form.Length;
            }
        }
        return 0;
    }

    private static readonly string[] s_lineBreaks = ["<br>", "<br/>", "<br />"];
}
