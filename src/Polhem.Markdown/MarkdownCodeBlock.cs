namespace Polhem.Markdown;

/// <summary>
/// A code block of the text a viewer shows, as a <see cref="MarkdownCodeBlockAction"/> receives it.
/// </summary>
public sealed class MarkdownCodeBlock
{
    internal MarkdownCodeBlock(string code, string language)
    {
        Code = code;
        Language = language;
    }

    /// <summary>Gets the code, as the copy button copies it.</summary>
    public string Code { get; }

    /// <summary>
    /// Gets the info string of the block's fence, such as <c>csharp</c> or <c>markdown</c>, as the viewer shows it above
    /// the code, or an empty string when the fence has none or the block is indented.
    /// </summary>
    public string Language { get; }
}
