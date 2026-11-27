using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit.Editing;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class MarkdownEditorSelectionTests(HeadlessSession session)
{
    private static MarkdownEditor Show(string markdown)
    {
        var editor = new MarkdownEditor { Markdown = markdown };
        var window = new Window { Width = 600, Height = 400, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    private static void Type(MarkdownEditor editor, string text)
    {
        var area = editor.TextEditor.TextArea;
        foreach (var character in text)
        {
            area.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = character.ToString() });
        }
    }

    private static Func<int> CountSelectionChanged(MarkdownEditor editor)
    {
        var count = 0;
        editor.SelectionChanged += (_, _) => count++;
        return () => count;
    }

    private static Func<int> CountMarkdownChanged(MarkdownEditor editor)
    {
        var count = 0;
        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == MarkdownEditor.MarkdownProperty)
            {
                count++;
            }
        };
        return () => count;
    }

    [Fact(DisplayName = "Select selects the range, puts the caret at its end and reports it once")]
    public Task Select_SetsSelectionAndCaret() => session.RunAsync(() =>
    {
        var editor = Show("one two three");
        var changes = CountSelectionChanged(editor);

        editor.Select(4, 3);

        Assert.Equal(4, editor.SelectionStart);
        Assert.Equal(3, editor.SelectionLength);
        Assert.Equal("two", editor.SelectedText);
        Assert.Equal(7, editor.CaretOffset);
        Assert.Equal(1, changes());
    });

    [Fact(DisplayName = "Select with a length of 0 clears the selection and moves the caret")]
    public Task Select_ZeroLength_MovesCaret() => session.RunAsync(() =>
    {
        var editor = Show("one two three");
        editor.Select(4, 3);

        editor.Select(2, 0);

        Assert.Equal(2, editor.SelectionStart);
        Assert.Equal(0, editor.SelectionLength);
        Assert.Equal("", editor.SelectedText);
        Assert.Equal(2, editor.CaretOffset);
    });

    [Fact(DisplayName = "Select rejects a range outside the text")]
    public Task Select_OutsideText_Throws() => session.RunAsync(() =>
    {
        var editor = Show("abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Select(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Select(2, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Select(4, 0));
    });

    [Fact(DisplayName = "Select scrolls the range into view")]
    public Task Select_ScrollsIntoView() => session.RunAsync(() =>
    {
        var editor = Show(string.Join("\n", Enumerable.Range(1, 200).Select(i => $"line {i}")));
        var textView = editor.TextEditor.TextArea.TextView;
        Assert.Equal(0, textView.ScrollOffset.Y);

        var start = editor.Markdown!.IndexOf("line 180", StringComparison.Ordinal);
        editor.Select(start, 8);
        Dispatcher.UIThread.RunJobs();

        var line = editor.TextEditor.Document.GetLineByOffset(start);
        var top = textView.GetVisualTopByDocumentLine(line.LineNumber);
        Assert.InRange(top, textView.ScrollOffset.Y, textView.ScrollOffset.Y + textView.Bounds.Height);
    });

    [Fact(DisplayName = "Moving the caret and selecting as the user does report the selection once per change")]
    public Task UserSelection_RaisesSelectionChanged() => session.RunAsync(() =>
    {
        var editor = Show("one two");
        var changes = CountSelectionChanged(editor);

        editor.CaretOffset = 3;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, changes());
        editor.CaretOffset = 3;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, changes());

        // NOTE: Shift+Left moves the caret, then sets the selection, and each raises an event of its own in AvaloniaEdit.
        // The editor reports the two as one.
        var area = editor.TextEditor.TextArea;
        area.Caret.Offset = 2;
        area.Selection = Selection.Create(area, 3, 2);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, editor.SelectionStart);
        Assert.Equal(1, editor.SelectionLength);
        Assert.Equal(2, changes());
    });

    [Fact(DisplayName = "Typing a character reports the caret once, after the key is handled")]
    public Task Type_RaisesSelectionChangedOnce() => session.RunAsync(() =>
    {
        var editor = Show("ab");
        editor.CaretOffset = 1;
        Dispatcher.UIThread.RunJobs();
        var changes = CountSelectionChanged(editor);

        Type(editor, "x");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("axb", editor.Markdown);
        Assert.Equal(2, editor.CaretOffset);
        Assert.Equal(1, changes());
    });

    [Fact(DisplayName = "A format from the toolbar reports the selection once")]
    public Task ApplyFormat_RaisesSelectionChangedOnce() => session.RunAsync(() =>
    {
        var editor = Show("one two");
        editor.Select(4, 3);
        var changes = CountSelectionChanged(editor);

        editor.ApplyFormat(MarkdownFormat.Bold);

        Assert.Equal("one **two**", editor.Markdown);
        Assert.Equal("two", editor.SelectedText);
        Assert.Equal(1, changes());
    });

    [Fact(DisplayName = "Replace changes a range as one step to undo, keeping the history before it")]
    public Task Replace_IsOneUndoStep() => session.RunAsync(() =>
    {
        var editor = Show("a");
        editor.CaretOffset = 1;
        Type(editor, "b");

        editor.Replace(0, 1, "x");

        Assert.Equal("xb", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal("ab", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal("a", editor.Markdown);
        Assert.True(editor.Redo());
        Assert.True(editor.Redo());
        Assert.Equal("xb", editor.Markdown);
    });

    [Fact(DisplayName = "Replace with several ranges takes offsets in the text before them, and one undo restores it")]
    public Task Replace_Several_IsOneUndoStep() => session.RunAsync(() =>
    {
        const string original = "one two three four";
        var editor = Show(original);
        var markdownChanges = CountMarkdownChanged(editor);

        editor.Replace(
        [
            new MarkdownReplacement(14, 4, "4"),
            new MarkdownReplacement(0, 3, "ONE"),
            new MarkdownReplacement(4, 4, ""),
            new MarkdownReplacement(13, 0, "!"),
        ]);

        Assert.Equal("ONE three! 4", editor.Markdown);
        Assert.Equal(1, markdownChanges());

        Assert.True(editor.Undo());
        Assert.Equal(original, editor.Markdown);
        Assert.False(editor.Undo());

        Assert.True(editor.Redo());
        Assert.Equal("ONE three! 4", editor.Markdown);
    });

    [Fact(DisplayName = "Typing after Replace is a step of its own")]
    public Task Type_AfterReplace_UndoesSeparately() => session.RunAsync(() =>
    {
        var editor = Show("abc");
        editor.Replace([new MarkdownReplacement(0, 1, "A"), new MarkdownReplacement(2, 1, "C")]);
        editor.CaretOffset = 3;

        Type(editor, "d");

        Assert.Equal("AbCd", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal("AbC", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal("abc", editor.Markdown);
    });

    [Fact(DisplayName = "The caret and the selection follow a replacement before them")]
    public Task Replace_Before_MovesCaretAndSelection() => session.RunAsync(() =>
    {
        var editor = Show("one two three");
        editor.Select(8, 5);
        var changes = CountSelectionChanged(editor);

        editor.Replace([new MarkdownReplacement(0, 3, "1"), new MarkdownReplacement(4, 0, "and ")]);

        Assert.Equal("1 and two three", editor.Markdown);
        Assert.Equal("three", editor.SelectedText);
        Assert.Equal(10, editor.SelectionStart);
        Assert.Equal(15, editor.CaretOffset);
        Assert.Equal(1, changes());
    });

    [Fact(DisplayName = "The caret and the selection stay put for a replacement after them")]
    public Task Replace_After_LeavesCaretAndSelection() => session.RunAsync(() =>
    {
        var editor = Show("one two three");
        editor.Select(0, 3);
        var changes = CountSelectionChanged(editor);

        editor.Replace(8, 5, "3");

        Assert.Equal("one two 3", editor.Markdown);
        Assert.Equal("one", editor.SelectedText);
        Assert.Equal(0, changes());
    });

    [Fact(DisplayName = "Replace changes the text of a read-only editor too")]
    public Task Replace_ReadOnly_Changes() => session.RunAsync(() =>
    {
        var editor = Show("abc");
        editor.IsReadOnly = true;

        editor.Replace(1, 1, "B");

        Assert.Equal("aBc", editor.Markdown);
    });

    [Fact(DisplayName = "Replace accepts ranges that touch and an insertion at the start of a range")]
    public Task Replace_TouchingRanges_Accepted() => session.RunAsync(() =>
    {
        var editor = Show("abcd");

        editor.Replace(
        [
            new MarkdownReplacement(1, 1, "B"),
            new MarkdownReplacement(2, 1, "C"),
            new MarkdownReplacement(1, 0, "["),
            new MarkdownReplacement(3, 0, "]"),
        ]);

        Assert.Equal("a[BC]d", editor.Markdown);
    });

    [Fact(DisplayName = "Replace rejects overlapping ranges and changes nothing")]
    public Task Replace_Overlapping_Throws() => session.RunAsync(() =>
    {
        var editor = Show("abcdef");

        Assert.Throws<ArgumentException>(() => editor.Replace(
            [new MarkdownReplacement(0, 3, "x"), new MarkdownReplacement(2, 2, "y")]));
        Assert.Throws<ArgumentException>(() => editor.Replace(
            [new MarkdownReplacement(2, 0, "x"), new MarkdownReplacement(2, 0, "y")]));
        Assert.Throws<ArgumentException>(() => editor.Replace(
            [new MarkdownReplacement(1, 4, "x"), new MarkdownReplacement(2, 0, "y")]));

        Assert.Equal("abcdef", editor.Markdown);
        Assert.False(editor.Undo());
    });

    [Fact(DisplayName = "Replace rejects a range outside the text and a missing text, and changes nothing")]
    public Task Replace_Invalid_Throws() => session.RunAsync(() =>
    {
        var editor = Show("abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Replace(
            [new MarkdownReplacement(0, 1, "x"), new MarkdownReplacement(2, 2, "y")]));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.Replace(-1, 0, "x"));
        Assert.Throws<ArgumentNullException>(() => editor.Replace(0, 1, null!));
        Assert.Throws<ArgumentNullException>(() => editor.Replace(null!));

        Assert.Equal("abc", editor.Markdown);
    });

    [Fact(DisplayName = "Replace with no ranges changes nothing")]
    public Task Replace_Empty_DoesNothing() => session.RunAsync(() =>
    {
        var editor = Show("abc");
        var markdownChanges = CountMarkdownChanged(editor);

        editor.Replace([]);

        Assert.Equal(0, markdownChanges());
        Assert.False(editor.Undo());
    });

    [Fact(DisplayName = "Undo of a replacement keeps Markdown in step and reports it once")]
    public Task Undo_Replace_ReportsMarkdownOnce() => session.RunAsync(() =>
    {
        var editor = Show("abc");
        editor.Replace([new MarkdownReplacement(0, 1, "A"), new MarkdownReplacement(2, 1, "C")]);
        var markdownChanges = CountMarkdownChanged(editor);

        Assert.True(editor.Undo());

        Assert.Equal("abc", editor.Markdown);
        Assert.Equal(1, markdownChanges());
    });
}
