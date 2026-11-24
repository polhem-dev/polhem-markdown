namespace Polhem.Markdown;

/// <summary>A format a Markdown editor applies to the selection, or removes when the selection already has it.</summary>
public enum MarkdownFormat
{
    /// <summary>Strong emphasis: <c>**text**</c>.</summary>
    Bold,

    /// <summary>Emphasis: <c>*text*</c>.</summary>
    Italic,

    /// <summary>Strikethrough: <c>~~text~~</c>.</summary>
    Strikethrough,

    /// <summary>Inline code: <c>`text`</c>.</summary>
    InlineCode,

    /// <summary>A link: <c>[text](url)</c>, with the address selected to type over.</summary>
    Link,

    /// <summary>A level 1 heading on each selected line: <c># text</c>.</summary>
    Heading1,

    /// <summary>A level 2 heading on each selected line: <c>## text</c>.</summary>
    Heading2,

    /// <summary>A level 3 heading on each selected line: <c>### text</c>.</summary>
    Heading3,

    /// <summary>A bulleted list item on each selected line: <c>- text</c>.</summary>
    BulletList,

    /// <summary>A numbered list item on each selected line, numbered from 1: <c>1. text</c>.</summary>
    NumberedList,

    /// <summary>A task list item on each selected line: <c>- [ ] text</c>.</summary>
    TaskList,

    /// <summary>A quote on each selected line: <c>&gt; text</c>.</summary>
    Quote,

    /// <summary>A fenced code block around the selected lines.</summary>
    CodeBlock,
}
