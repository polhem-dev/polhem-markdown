namespace Polhem.Markdown.Editing;

/// <summary>What a stretch of Markdown source is, which decides its color in the editor.</summary>
internal enum MarkdownSyntaxKind : byte
{
    /// <summary>Text with no syntax of its own.</summary>
    None,

    /// <summary>
    /// Punctuation that is syntax rather than text: heading and emphasis markers, code fences, list bullets, quote
    /// markers, task boxes, table pipes, link brackets and thematic breaks.
    /// </summary>
    Marker,

    /// <summary>The text of a heading.</summary>
    Heading,

    /// <summary>Inline code, or the code of a block that is not highlighted.</summary>
    Code,

    /// <summary>The text of a link or an image, or a footnote reference.</summary>
    Link,

    /// <summary>The destination or title of a link, an image or a link reference definition, or a bare URL.</summary>
    Url,

    /// <summary>Raw HTML that no highlighted token covers.</summary>
    Html,

    /// <summary>A formula.</summary>
    Math,

    /// <summary>A keyword in highlighted code.</summary>
    CodeKeyword,

    /// <summary>A string in highlighted code.</summary>
    CodeString,

    /// <summary>A comment in highlighted code.</summary>
    CodeComment,

    /// <summary>A number or constant in highlighted code.</summary>
    CodeConstant,

    /// <summary>A markup tag name in highlighted code or raw HTML.</summary>
    CodeTag,

    /// <summary>An attribute name, key, CSS property or shell variable in highlighted code or raw HTML.</summary>
    CodeProperty,
}
