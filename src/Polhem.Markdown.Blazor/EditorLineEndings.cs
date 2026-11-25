namespace Polhem.Markdown.Blazor;

/// <summary>
/// Line endings between the text of a host and the textarea of <see cref="MarkdownEditor"/>.
/// </summary>
/// <remarks>
/// A textarea gives every line break back as <c>\n</c>, whatever its text was given, so the editor works on that form
/// and hands the host its text back with <c>\r\n</c> when that is what the host's text used. A lone <c>\r</c> becomes
/// <c>\n</c> for good.
/// </remarks>
internal static class EditorLineEndings
{
    /// <summary>The text as a textarea holds it: every line break a <c>\n</c>.</summary>
    public static string ToTextarea(string text) =>
        text.Contains('\r', StringComparison.Ordinal)
            ? text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n')
            : text;

    /// <summary>Whether <paramref name="text"/> ends its lines with <c>\r\n</c>.</summary>
    public static bool UsesCrLf(string text) => text.Contains("\r\n", StringComparison.Ordinal);

    /// <summary>The text of the textarea with the line breaks the host's text used.</summary>
    public static string ToHost(string text, bool crLf) =>
        crLf ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;

    /// <summary>
    /// The offset in the host's text of <paramref name="offset"/> in the textarea's <paramref name="text"/>: one more for
    /// every line break before it when the host's text uses <c>\r\n</c>.
    /// </summary>
    public static int ToHostOffset(string text, int offset, bool crLf)
    {
        offset = Math.Clamp(offset, 0, text.Length);
        return crLf ? offset + text.AsSpan(0, offset).Count('\n') : offset;
    }

    /// <summary>
    /// The offset in the textarea's <paramref name="text"/> of <paramref name="hostOffset"/> in the host's text. An offset
    /// between the <c>\r</c> and the <c>\n</c> of a line break is taken as the start of the break.
    /// </summary>
    public static int ToTextareaOffset(string text, int hostOffset, bool crLf)
    {
        if (!crLf)
        {
            return Math.Clamp(hostOffset, 0, text.Length);
        }
        var host = 0;
        for (var index = 0; index < text.Length; index++)
        {
            host += text[index] == '\n' ? 2 : 1;
            if (host > hostOffset)
            {
                return index;
            }
        }
        return text.Length;
    }
}
