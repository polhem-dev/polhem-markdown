namespace Polhem.Markdown;

/// <summary>A button of a Markdown editor's toolbar.</summary>
public enum MarkdownToolbarItem
{
    /// <summary>Applies <see cref="MarkdownFormat.Bold"/>.</summary>
    Bold,

    /// <summary>Applies <see cref="MarkdownFormat.Italic"/>.</summary>
    Italic,

    /// <summary>Applies <see cref="MarkdownFormat.Strikethrough"/>.</summary>
    Strikethrough,

    /// <summary>Makes the selected lines headings one level deeper, from plain text to level 3 and back to plain text.</summary>
    Heading,

    /// <summary>Applies <see cref="MarkdownFormat.InlineCode"/>.</summary>
    InlineCode,

    /// <summary>Applies <see cref="MarkdownFormat.CodeBlock"/>.</summary>
    CodeBlock,

    /// <summary>Applies <see cref="MarkdownFormat.Quote"/>.</summary>
    Quote,

    /// <summary>Applies <see cref="MarkdownFormat.BulletList"/>.</summary>
    BulletList,

    /// <summary>Applies <see cref="MarkdownFormat.NumberedList"/>.</summary>
    NumberedList,

    /// <summary>Applies <see cref="MarkdownFormat.TaskList"/>.</summary>
    TaskList,

    /// <summary>Applies <see cref="MarkdownFormat.Link"/>.</summary>
    Link,

    /// <summary>Applies <see cref="MarkdownFormat.Image"/>.</summary>
    Image,

    /// <summary>Applies <see cref="MarkdownFormat.Table"/>.</summary>
    Table,

    /// <summary>Applies <see cref="MarkdownFormat.HorizontalRule"/>.</summary>
    HorizontalRule,

    /// <summary>Undoes the last edit.</summary>
    Undo,

    /// <summary>Redoes the last edit undone.</summary>
    Redo,
}
