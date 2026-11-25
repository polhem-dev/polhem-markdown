using Avalonia.Automation;
using Avalonia.Headless;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class MarkdownEditorToolbarTests(HeadlessSession session)
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

    private static void Click(MarkdownEditor editor, MarkdownToolbarItem item)
    {
        editor.ToolbarButton(item).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Control control, Key key) =>
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    [Fact(DisplayName = "The toolbar shows above the text, with a button for every item in the shared order")]
    public Task Toolbar_ShowsEveryItemAboveText() => session.RunAsync(() =>
    {
        var editor = Show("text");

        var buttons = editor.Toolbar.GetVisualDescendants().OfType<Button>().ToList();
        var expected = ToolbarItems.Groups.SelectMany(group => group).Select(editor.ToolbarButton).ToList();

        Assert.Equal(expected, buttons);
        Assert.True(editor.Toolbar.IsVisible);
        Assert.True(editor.Toolbar.Bounds.Height > 0);
        Assert.Equal(editor.Toolbar.Bounds.Height, editor.TextEditor.Bounds.Top);
        Assert.Equal(editor.Bounds.Height, editor.Toolbar.Bounds.Height + editor.TextEditor.Bounds.Height);
    });

    [Fact(DisplayName = "Without the toolbar the text takes the whole editor")]
    public Task ShowToolbar_False_HidesToolbar() => session.RunAsync(() =>
    {
        var editor = Show("text");

        editor.ShowToolbar = false;
        Dispatcher.UIThread.RunJobs();

        Assert.False(editor.Toolbar.IsVisible);
        Assert.Equal(0, editor.TextEditor.Bounds.Top);
        Assert.Equal(editor.Bounds.Height, editor.TextEditor.Bounds.Height);
    });

    [Fact(DisplayName = "A format button formats the selection and leaves the focus in the text")]
    public Task FormatButton_FormatsSelection() => session.RunAsync(() =>
    {
        var editor = Show("a word b");
        editor.TextEditor.Select(2, 4);

        Click(editor, MarkdownToolbarItem.Bold);

        Assert.Equal("a **word** b", editor.Markdown);
        Assert.Equal("word", editor.TextEditor.SelectedText);
        Assert.True(editor.TextEditor.TextArea.IsFocused);
    });

    [Fact(DisplayName = "The heading button steps the line through the heading levels")]
    public Task HeadingButton_CyclesHeading() => session.RunAsync(() =>
    {
        var editor = Show("title");

        Click(editor, MarkdownToolbarItem.Heading);
        Click(editor, MarkdownToolbarItem.Heading);

        Assert.Equal("## title", editor.Markdown);
    });

    [Fact(DisplayName = "Undo and redo are enabled only with a step to take, and take it")]
    public Task UndoRedoButtons_FollowHistory() => session.RunAsync(() =>
    {
        var editor = Show("text");
        var undo = editor.ToolbarButton(MarkdownToolbarItem.Undo);
        var redo = editor.ToolbarButton(MarkdownToolbarItem.Redo);
        Assert.False(undo.IsEnabled);
        Assert.False(redo.IsEnabled);

        editor.TextEditor.Select(0, 4);
        Click(editor, MarkdownToolbarItem.Italic);
        Assert.True(undo.IsEnabled);

        Click(editor, MarkdownToolbarItem.Undo);
        Assert.Equal("text", editor.Markdown);
        Assert.False(undo.IsEnabled);
        Assert.True(redo.IsEnabled);

        Click(editor, MarkdownToolbarItem.Redo);
        Assert.Equal("*text*", editor.Markdown);
    });

    [Fact(DisplayName = "Every button is disabled while the editor is read-only")]
    public Task ReadOnly_DisablesButtons() => session.RunAsync(() =>
    {
        var editor = Show("text");
        editor.TextEditor.Select(0, 4);
        editor.ApplyFormat(MarkdownFormat.Bold);

        editor.IsReadOnly = true;

        Assert.All(Enum.GetValues<MarkdownToolbarItem>(), item => Assert.False(editor.ToolbarButton(item).IsEnabled));

        editor.IsReadOnly = false;

        Assert.True(editor.ToolbarButton(MarkdownToolbarItem.Bold).IsEnabled);
        Assert.True(editor.ToolbarButton(MarkdownToolbarItem.Undo).IsEnabled);
    });

    [Fact(DisplayName = "A button is named by its label, and its tooltip adds the shortcut")]
    public Task Labels_DefaultToEnglishWithShortcut() => session.RunAsync(() =>
    {
        var editor = Show("text");
        var bold = editor.ToolbarButton(MarkdownToolbarItem.Bold);

        Assert.Equal("Bold", AutomationProperties.GetName(bold));
        Assert.StartsWith("Bold (", ToolTip.GetTip(bold) as string, StringComparison.Ordinal);
        Assert.Equal("Table", ToolTip.GetTip(editor.ToolbarButton(MarkdownToolbarItem.Table)));
    });

    [Fact(DisplayName = "The host's labels replace the English ones, and null keeps the English one")]
    public Task ToolbarLabels_ReplaceLabels() => session.RunAsync(() =>
    {
        var editor = Show("text");

        editor.ToolbarLabels = item => item == MarkdownToolbarItem.Table ? "表格" : null;

        var table = editor.ToolbarButton(MarkdownToolbarItem.Table);
        Assert.Equal("表格", AutomationProperties.GetName(table));
        Assert.Equal("表格", ToolTip.GetTip(table));
        Assert.Equal("Bold", AutomationProperties.GetName(editor.ToolbarButton(MarkdownToolbarItem.Bold)));
    });

    [Fact(DisplayName = "The arrow keys move between the enabled buttons, and Escape goes back to the text")]
    public Task ArrowKeys_MoveBetweenButtons() => session.RunAsync(() =>
    {
        var editor = Show("text");
        var bold = editor.ToolbarButton(MarkdownToolbarItem.Bold);
        bold.Focus();

        Press(bold, Key.Right);
        Assert.True(editor.ToolbarButton(MarkdownToolbarItem.Italic).IsFocused);

        // Undo and redo are disabled with no history, so End lands on the last button that can do something.
        Press(editor.ToolbarButton(MarkdownToolbarItem.Italic), Key.End);
        Assert.True(editor.ToolbarButton(MarkdownToolbarItem.HorizontalRule).IsFocused);

        Press(editor.ToolbarButton(MarkdownToolbarItem.HorizontalRule), Key.Right);
        Assert.True(bold.IsFocused);

        Press(bold, Key.Escape);
        Assert.True(editor.TextEditor.TextArea.IsFocused);
    });

    [Fact(DisplayName = "Tab passes the editor itself by, and reaches the toolbar before the text")]
    public Task Tab_ReachesToolbarThenText() => session.RunAsync(() =>
    {
        var before = new Button { Content = "before" };
        var editor = new MarkdownEditor { Markdown = "text" };
        var panel = new DockPanel();
        DockPanel.SetDock(before, Dock.Top);
        panel.Children.Add(before);
        panel.Children.Add(editor);
        var window = new Window { Width = 600, Height = 400, Content = panel };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        before.Focus();

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        var toolbar = window.FocusManager?.GetFocusedElement();
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        var text = window.FocusManager?.GetFocusedElement();

        Assert.Same(editor.ToolbarButton(MarkdownToolbarItem.Bold), toolbar);
        Assert.Same(editor.TextEditor.TextArea, text);
    });
}
