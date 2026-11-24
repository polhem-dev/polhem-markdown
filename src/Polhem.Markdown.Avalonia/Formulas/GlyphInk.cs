namespace Polhem.Markdown.Avalonia.Formulas;

/// <summary>
/// Estimates how far a glyph's ink reaches above and below the baseline, in ems, by the kind of character: a lowercase
/// letter reaches the x-height, a capital or a digit the cap height, a bracket below the baseline, an accent mark sits
/// high. The text engine gives neither ink bounds nor glyph outlines on every platform, and its line height would float
/// an accent well above a lowercase letter; text fonts differ little enough in these proportions for math to read well.
/// </summary>
internal static class GlyphInk
{
    private const double XHeight = 0.52;
    private const double CapHeight = 0.72;
    private const double Descender = -0.21;

    /// <summary>The highest and lowest ink of <paramref name="text"/>, in ems above the baseline; nothing for blank text.</summary>
    public static (double Top, double Bottom)? Of(string text)
    {
        (double Top, double Bottom)? ink = null;
        foreach (var c in text)
        {
            if (char.IsWhiteSpace(c))
            {
                continue;
            }
            var (top, bottom) = Of(c);
            ink = ink is { } so ? (Math.Max(so.Top, top), Math.Min(so.Bottom, bottom)) : (top, bottom);
        }
        return ink;
    }

    private static (double Top, double Bottom) Of(char c) => c switch
    {
        // Accent marks, drawn high above the baseline.
        'ˆ' or '¯' or '˙' or '˜' or '¨' => (0.75, 0.55),
        '′' => (0.75, 0.42),
        'a' or 'c' or 'e' or 'm' or 'n' or 'o' or 'r' or 's' or 'u' or 'v' or 'w' or 'x' or 'z' => (XHeight, 0),
        'g' or 'p' or 'q' or 'y' => (XHeight, Descender),
        'j' => (CapHeight, Descender),
        'b' or 'd' or 'f' or 'h' or 'i' or 'k' or 'l' or 't' => (CapHeight, 0),
        'α' or 'ε' or 'ϵ' or 'ι' or 'κ' or 'ν' or 'ο' or 'π' or 'σ' or 'τ' or 'υ' or 'ω' or 'ϖ' or 'ς' => (XHeight, 0),
        'γ' or 'η' or 'μ' or 'ρ' or 'χ' or 'ϱ' => (XHeight, Descender),
        'β' or 'ζ' or 'ξ' or 'φ' or 'ψ' or 'ϕ' => (CapHeight, Descender),
        'δ' or 'θ' or 'λ' or 'ϑ' or 'ℓ' or '∂' => (CapHeight, 0),
        '(' or ')' or '[' or ']' or '{' or '}' or '|' or '‖' or '⟨' or '⟩' or '⌊' or '⌋' or '⌈' or '⌉' or '/' or '\\' => (0.76, -0.24),
        '∑' or '∏' or '∐' or '⋃' or '⋂' or '⨁' or '⨂' or '⋁' or '⋀' or '∫' or '∬' or '∭' or '∮' => (0.76, -0.24),
        ',' or ';' => (0.12, -0.16),
        '.' or '…' => (0.1, 0),
        ':' => (XHeight, 0),
        '∞' or '∘' or '∙' or '⋅' => (0.45, 0.05),
        _ when char.IsAsciiDigit(c) || char.IsUpper(c) => (CapHeight, 0),
        _ when char.IsLower(c) => (XHeight, 0),
        _ when char.IsLetter(c) => (CapHeight, 0),
        // Operators, relations and arrows lie about the math axis.
        _ => (0.58, -0.06),
    };
}
