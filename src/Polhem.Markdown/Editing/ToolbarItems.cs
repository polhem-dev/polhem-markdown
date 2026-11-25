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
    /// What each button does, its English label, and its icon. An icon is SVG path data on a 16 by 16 grid, drawn as lines
    /// 1.5 wide with round ends and joins, and not filled; the icons are drawn for this package, so no icon set and no
    /// licence of one comes with it.
    /// </summary>
    private static readonly Dictionary<MarkdownToolbarItem, Entry> s_entries = new()
    {
        [MarkdownToolbarItem.Bold] = new("Bold", MarkdownFormat.Bold,
            "M4.5 2.5h4a2.75 2.75 0 0 1 0 5.5h-4z M4.5 8h4.75a2.75 2.75 0 0 1 0 5.5H4.5z"),
        [MarkdownToolbarItem.Italic] = new("Italic", MarkdownFormat.Italic,
            "M7 2.5h5 M4 13.5h5 M9.5 2.5l-3 11"),
        [MarkdownToolbarItem.Strikethrough] = new("Strikethrough", MarkdownFormat.Strikethrough,
            "M11.5 4.5C11 3.3 9.8 2.5 8 2.5c-2.2 0-3.5 1.2-3.5 2.6 0 1 .6 1.7 1.6 2.2 M2 8.5h12 M4.5 11.5c.5 1.3 1.8 2 3.5 2 2.2 0 3.5-1.1 3.5-2.6"),
        [MarkdownToolbarItem.Heading] = new("Heading", null,
            "M3.5 2.5v11 M12.5 2.5v11 M3.5 8h9"),
        [MarkdownToolbarItem.InlineCode] = new("Inline code", MarkdownFormat.InlineCode,
            "M5.5 4L1.5 8l4 4 M10.5 4l4 4-4 4"),
        [MarkdownToolbarItem.CodeBlock] = new("Code block", MarkdownFormat.CodeBlock,
            "M2 2.5h12v11H2z M6.5 6L4.5 8l2 2 M9.5 6l2 2-2 2"),
        [MarkdownToolbarItem.Quote] = new("Quote", MarkdownFormat.Quote,
            "M3 3v10 M6.5 4.5h7 M6.5 8h7 M6.5 11.5h5"),
        [MarkdownToolbarItem.BulletList] = new("Bulleted list", MarkdownFormat.BulletList,
            "M2.5 4h.01 M2.5 8h.01 M2.5 12h.01 M6 4h8 M6 8h8 M6 12h8"),
        [MarkdownToolbarItem.NumberedList] = new("Numbered list", MarkdownFormat.NumberedList,
            "M2 2.5l1-.5v4 M1.75 9.25c0-1.3 2.5-1.3 2.5 0 0 .9-2.5 1.9-2.5 3.25h2.5 M6.5 4h7.5 M6.5 8h7.5 M6.5 12h7.5"),
        [MarkdownToolbarItem.TaskList] = new("Task list", MarkdownFormat.TaskList,
            "M1.75 2.5h4v4h-4z M2.75 4.5l1 1 1.5-2 M8.5 4.5h5.5 M1.75 9.5h4v4h-4z M8.5 11.5h5.5"),
        [MarkdownToolbarItem.Link] = new("Link", MarkdownFormat.Link,
            "M6.5 9.5l3-3 M7 4.5l1.25-1.25a2.65 2.65 0 0 1 3.75 3.75L10.75 8.25 M9 11.5l-1.25 1.25a2.65 2.65 0 0 1-3.75-3.75L5.25 7.75"),
        [MarkdownToolbarItem.Image] = new("Image", MarkdownFormat.Image,
            "M2 2.5h12v11H2z M2 11l3.5-3.5 3 3 2-2 3.5 3.5 M10.5 5.5h.01"),
        [MarkdownToolbarItem.Table] = new("Table", MarkdownFormat.Table,
            "M2 2.5h12v11H2z M2 6h12 M2 9.75h12 M8 2.5v11"),
        [MarkdownToolbarItem.HorizontalRule] = new("Horizontal rule", MarkdownFormat.HorizontalRule,
            "M1.5 8h13 M4 4h8 M4 12h8"),
        [MarkdownToolbarItem.Undo] = new("Undo", null,
            "M5 2.5L2 5.5l3 3 M2 5.5h7.5a3.75 3.75 0 0 1 0 7.5H6"),
        [MarkdownToolbarItem.Redo] = new("Redo", null,
            "M11 2.5l3 3-3 3 M14 5.5H6.5a3.75 3.75 0 0 0 0 7.5H10"),
    };

    /// <summary>
    /// The format <paramref name="item"/> applies, or <see langword="null"/> for <see cref="MarkdownToolbarItem.Heading"/>,
    /// <see cref="MarkdownToolbarItem.Undo"/> and <see cref="MarkdownToolbarItem.Redo"/>, which are commands of their own.
    /// </summary>
    public static MarkdownFormat? Format(MarkdownToolbarItem item) => EntryOf(item).Format;

    /// <summary>The English label of <paramref name="item"/>, which a host may replace.</summary>
    public static string Label(MarkdownToolbarItem item) => EntryOf(item).Label;

    /// <summary>The icon of <paramref name="item"/>, as SVG path data.</summary>
    public static string Icon(MarkdownToolbarItem item) => EntryOf(item).Icon;

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

    private static Entry EntryOf(MarkdownToolbarItem item) =>
        s_entries.TryGetValue(item, out var entry) ? entry : throw new ArgumentOutOfRangeException(nameof(item), item, null);

    private sealed record Entry(string Label, MarkdownFormat? Format, string Icon);
}
