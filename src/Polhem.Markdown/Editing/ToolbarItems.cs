namespace Polhem.Markdown.Editing;

/// <summary>
/// The buttons of the editors' toolbars, in order and in groups, with what each does and its default label, so the
/// toolbars of the Avalonia and Blazor editors are the same.
/// </summary>
internal static class ToolbarItems
{
    /// <summary>The buttons, in the groups a toolbar separates, in the order it shows them.</summary>
    public static IReadOnlyList<IReadOnlyList<MarkdownToolbarItem>> Groups { get; } =
    [
        [MarkdownToolbarItem.Bold, MarkdownToolbarItem.Italic, MarkdownToolbarItem.Strikethrough],
        [MarkdownToolbarItem.Heading],
        [MarkdownToolbarItem.InlineCode, MarkdownToolbarItem.CodeBlock, MarkdownToolbarItem.Quote],
        [MarkdownToolbarItem.BulletList, MarkdownToolbarItem.NumberedList, MarkdownToolbarItem.TaskList],
        [MarkdownToolbarItem.Link, MarkdownToolbarItem.Image, MarkdownToolbarItem.Table, MarkdownToolbarItem.HorizontalRule],
        [MarkdownToolbarItem.Undo, MarkdownToolbarItem.Redo],
    ];

    /// <summary>
    /// The format <paramref name="item"/> applies, or <see langword="null"/> for <see cref="MarkdownToolbarItem.Heading"/>,
    /// <see cref="MarkdownToolbarItem.Undo"/> and <see cref="MarkdownToolbarItem.Redo"/>, which are commands of their own.
    /// </summary>
    public static MarkdownFormat? Format(MarkdownToolbarItem item) => item switch
    {
        MarkdownToolbarItem.Bold => MarkdownFormat.Bold,
        MarkdownToolbarItem.Italic => MarkdownFormat.Italic,
        MarkdownToolbarItem.Strikethrough => MarkdownFormat.Strikethrough,
        MarkdownToolbarItem.InlineCode => MarkdownFormat.InlineCode,
        MarkdownToolbarItem.CodeBlock => MarkdownFormat.CodeBlock,
        MarkdownToolbarItem.Quote => MarkdownFormat.Quote,
        MarkdownToolbarItem.BulletList => MarkdownFormat.BulletList,
        MarkdownToolbarItem.NumberedList => MarkdownFormat.NumberedList,
        MarkdownToolbarItem.TaskList => MarkdownFormat.TaskList,
        MarkdownToolbarItem.Link => MarkdownFormat.Link,
        MarkdownToolbarItem.Image => MarkdownFormat.Image,
        MarkdownToolbarItem.Table => MarkdownFormat.Table,
        MarkdownToolbarItem.HorizontalRule => MarkdownFormat.HorizontalRule,
        _ => null,
    };

    /// <summary>The English label of <paramref name="item"/>, which a host may replace.</summary>
    public static string Label(MarkdownToolbarItem item) => item switch
    {
        MarkdownToolbarItem.Bold => "Bold",
        MarkdownToolbarItem.Italic => "Italic",
        MarkdownToolbarItem.Strikethrough => "Strikethrough",
        MarkdownToolbarItem.Heading => "Heading",
        MarkdownToolbarItem.InlineCode => "Inline code",
        MarkdownToolbarItem.CodeBlock => "Code block",
        MarkdownToolbarItem.Quote => "Quote",
        MarkdownToolbarItem.BulletList => "Bulleted list",
        MarkdownToolbarItem.NumberedList => "Numbered list",
        MarkdownToolbarItem.TaskList => "Task list",
        MarkdownToolbarItem.Link => "Link",
        MarkdownToolbarItem.Image => "Image",
        MarkdownToolbarItem.Table => "Table",
        MarkdownToolbarItem.HorizontalRule => "Horizontal rule",
        MarkdownToolbarItem.Undo => "Undo",
        MarkdownToolbarItem.Redo => "Redo",
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null),
    };

    /// <summary>
    /// The keyboard shortcut of <paramref name="item"/> as a toolbar shows it, written for macOS when
    /// <paramref name="mac"/> is set, or <see langword="null"/> when it has none.
    /// </summary>
    /// <remarks>The shortcuts are those the editors handle: Ctrl, or Cmd on macOS, with B, I, K, E and Z, and Ctrl+Y or Shift+Cmd+Z to redo.</remarks>
    public static string? Shortcut(MarkdownToolbarItem item, bool mac)
    {
        var key = item switch
        {
            MarkdownToolbarItem.Bold => "B",
            MarkdownToolbarItem.Italic => "I",
            MarkdownToolbarItem.InlineCode => "E",
            MarkdownToolbarItem.Link => "K",
            MarkdownToolbarItem.Undo => "Z",
            MarkdownToolbarItem.Redo => mac ? "Z" : "Y",
            _ => null,
        };
        if (key is null)
        {
            return null;
        }
        if (!mac)
        {
            return "Ctrl+" + key;
        }
        return item == MarkdownToolbarItem.Redo ? "⇧⌘" + key : "⌘" + key;
    }

    /// <summary>The tooltip of a button: <paramref name="label"/>, followed by the shortcut in parentheses if there is one.</summary>
    public static string Tooltip(MarkdownToolbarItem item, string label, bool mac) =>
        Shortcut(item, mac) is { } shortcut ? label + " (" + shortcut + ")" : label;
}
