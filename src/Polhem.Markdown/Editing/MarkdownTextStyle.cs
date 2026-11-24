namespace Polhem.Markdown.Editing;

/// <summary>The emphasis a stretch of Markdown source is written with, which may combine.</summary>
[Flags]
internal enum MarkdownTextStyle : byte
{
    /// <summary>No emphasis.</summary>
    None = 0,

    /// <summary>Strong emphasis (<c>**text**</c>).</summary>
    Bold = 1,

    /// <summary>Emphasis (<c>*text*</c>).</summary>
    Italic = 2,

    /// <summary>Strikethrough (<c>~~text~~</c>).</summary>
    Strikethrough = 4,
}
