namespace Polhem.Markdown.Avalonia;

/// <summary>How a <see cref="MarkdownRangeMark"/> is drawn over its range of the source.</summary>
public enum MarkdownRangeMarkStyle
{
    /// <summary>The range's background is filled with the mark's brush.</summary>
    Background,

    /// <summary>A line in the mark's brush runs under the range.</summary>
    Underline,

    /// <summary>A line in the mark's brush runs through the middle of the range.</summary>
    Strikethrough,
}
