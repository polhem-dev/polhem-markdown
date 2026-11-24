using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class MarkdownEditorCommandTests(HeadlessSession session)
{
    private static MarkdownEditor Show(string markdown)
    {
        var editor = new MarkdownEditor { Markdown = markdown };
        var window = new Window { Width = 600, Height = 400, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        editor.TextEditor.TextArea.Focus();
        return editor;
    }

    private static void Press(MarkdownEditor editor, Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        editor.TextEditor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    private static KeyModifiers Command =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    [Fact(DisplayName = "A format wraps the selection, keeps it selected, and one undo takes it back")]
    public Task ApplyFormat_IsOneUndoStep() => session.RunAsync(() =>
    {
        var editor = Show("a word b");
        editor.TextEditor.Select(2, 4);

        editor.ApplyFormat(MarkdownFormat.Bold);

        Assert.Equal("a **word** b", editor.Markdown);
        Assert.Equal("word", editor.TextEditor.SelectedText);
        Assert.True(editor.Undo());
        Assert.Equal("a word b", editor.Markdown);
    });

    [Theory(DisplayName = "The command shortcuts apply bold, italic, a link and inline code")]
    [InlineData(Key.B, "**x**")]
    [InlineData(Key.I, "*x*")]
    [InlineData(Key.K, "[x](url)")]
    [InlineData(Key.E, "`x`")]
    public Task Shortcut_AppliesFormat(Key key, string expected) => session.RunAsync(() =>
    {
        var editor = Show("x");
        editor.TextEditor.Select(0, 1);

        Press(editor, key, Command);

        Assert.Equal(expected, editor.Markdown);
    });

    [Fact(DisplayName = "Enter on a list item starts the next one, and on an empty item ends the list")]
    public Task Enter_ContinuesAndEndsList() => session.RunAsync(() =>
    {
        var editor = Show("1. one");
        editor.CaretOffset = 6;

        Press(editor, Key.Enter);
        Assert.Equal("1. one\n2. ", editor.Markdown);
        Assert.Equal(10, editor.CaretOffset);

        Press(editor, Key.Enter);
        Assert.Equal("1. one\n", editor.Markdown);
    });

    [Fact(DisplayName = "Enter outside a list inserts a plain new line")]
    public Task Enter_OutsideList_IsPlain() => session.RunAsync(() =>
    {
        var editor = Show("text");
        editor.CaretOffset = 4;

        Press(editor, Key.Enter);

        Assert.Equal("text\n", editor.Markdown);
    });

    [Fact(DisplayName = "Tab and Shift+Tab move a list item in and out")]
    public Task Tab_MovesListItem() => session.RunAsync(() =>
    {
        var editor = Show("- a\n- b");
        editor.CaretOffset = 7;

        Press(editor, Key.Tab);
        Assert.Equal("- a\n  - b", editor.Markdown);

        Press(editor, Key.Tab, KeyModifiers.Shift);
        Assert.Equal("- a\n- b", editor.Markdown);
    });

    [Fact(DisplayName = "A read-only editor applies no format and leaves the keys alone")]
    public Task ReadOnly_ChangesNothing() => session.RunAsync(() =>
    {
        var editor = Show("- a");
        editor.IsReadOnly = true;
        editor.TextEditor.Select(2, 1);

        editor.ApplyFormat(MarkdownFormat.Bold);
        Press(editor, Key.B, Command);
        editor.CaretOffset = 3;
        Press(editor, Key.Enter);

        Assert.Equal("- a", editor.Markdown);
    });
}
