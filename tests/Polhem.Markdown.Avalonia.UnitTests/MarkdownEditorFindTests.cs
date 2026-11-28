using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>Find and replace in the Avalonia editor (polhem-dev/polhem-markdown#97).</summary>
[Collection(HeadlessCollection.Name)]
public class MarkdownEditorFindTests(HeadlessSession session)
{
    private static (MarkdownEditor Editor, Window Window) Show(string markdown, double height = 400)
    {
        var editor = new MarkdownEditor { Markdown = markdown };
        var window = new Window { Width = 600, Height = height, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    private static RawInputModifiers Command =>
        Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers == KeyModifiers.Meta
            ? RawInputModifiers.Meta
            : RawInputModifiers.Control;

    private static bool IsMac => Command == RawInputModifiers.Meta;

    private static void Press(Window window, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        window.KeyPressQwerty(key, modifiers);
        Dispatcher.UIThread.RunJobs();
    }

    private static void PressReplaceShortcut(Window window)
    {
        if (IsMac)
        {
            Press(window, PhysicalKey.F, RawInputModifiers.Meta | RawInputModifiers.Alt);
        }
        else
        {
            Press(window, PhysicalKey.H, RawInputModifiers.Control);
        }
    }

    private static void FocusText(MarkdownEditor editor)
    {
        editor.TextEditor.TextArea.Focus();
        Dispatcher.UIThread.RunJobs();
    }

    [Fact(DisplayName = "The command shortcut with F opens the find bar over the text with the focus in it, and typing finds as it goes")]
    public Task CommandF_OpensBar_TypingFinds() => session.RunAsync(() =>
    {
        var (editor, window) = Show("cat dog\ncat\nCAT");
        FocusText(editor);

        Press(window, PhysicalKey.F, Command);

        var bar = editor.FindBarControl!;
        Assert.True(editor.IsFindOpen);
        Assert.False(editor.IsReplaceOpen);
        Assert.True(bar.Field.IsFocused);
        Assert.True(bar.Bounds.Y >= editor.Toolbar.Bounds.Bottom);

        window.KeyTextInput("cat");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(3, editor.FindMatchCount);
        Assert.Equal(1, editor.CurrentFindMatch);
        Assert.Equal("1/3", bar.CountText);
        Assert.Equal((0, 3), (editor.SelectionStart, editor.SelectionLength));
    });

    [Fact(DisplayName = "The selection on one line becomes the text to find")]
    public Task CommandF_WithSelection_FindsIt() => session.RunAsync(() =>
    {
        var (editor, window) = Show("one two one");
        FocusText(editor);
        editor.Select(0, 3);

        Press(window, PhysicalKey.F, Command);

        Assert.Equal("one", editor.FindBarControl!.Field.Text);
        Assert.Equal(2, editor.FindMatchCount);
    });

    [Fact(DisplayName = "The replace shortcut opens the bar with its replace row, and a read-only editor opens find only")]
    public Task ReplaceShortcut_OpensReplace() => session.RunAsync(() =>
    {
        var (editor, window) = Show("one two one");
        FocusText(editor);
        editor.Select(0, 3);

        PressReplaceShortcut(window);

        Assert.True(editor.IsReplaceOpen);
        Assert.True(editor.FindBarControl!.ReplaceField!.IsFocused);

        Press(window, PhysicalKey.Escape);
        editor.IsReadOnly = true;
        FocusText(editor);
        PressReplaceShortcut(window);

        Assert.True(editor.IsFindOpen);
        Assert.False(editor.IsReplaceOpen);
    });

    [Fact(DisplayName = "HandleFindShortcuts off leaves the keys to the host, and AvaloniaEdit's own search panel does not take them")]
    public Task HandleFindShortcutsOff_KeysPassThrough() => session.RunAsync(() =>
    {
        var (editor, window) = Show("text");
        editor.HandleFindShortcuts = false;
        var reached = 0;
        window.KeyDown += (_, e) =>
        {
            if (e.Key is Key.F or Key.H)
            {
                reached++;
            }
        };
        FocusText(editor);

        // NOTE: AvaloniaEdit's panel picks its keys by the operating system, not by the platform's command modifier,
        // so both modifiers are pressed whatever platform the tests run on.
        Press(window, PhysicalKey.F, RawInputModifiers.Control);
        Press(window, PhysicalKey.F, RawInputModifiers.Meta);
        Press(window, PhysicalKey.H, RawInputModifiers.Control);
        Press(window, PhysicalKey.F, RawInputModifiers.Meta | RawInputModifiers.Alt);

        Assert.False(editor.IsFindOpen);
        Assert.Equal(4, reached);
        Assert.False(editor.TextEditor.SearchPanel?.IsOpened ?? false);

        editor.OpenReplace();

        Assert.True(editor.IsReplaceOpen);
    });

    [Fact(DisplayName = "Enter and Shift+Enter in the bar, F3 and Shift+F3 go through the matches and wrap around")]
    public Task Keys_StepThroughMatches() => session.RunAsync(() =>
    {
        var (editor, window) = Show("a x a x a");
        FocusText(editor);
        editor.OpenFind("a");
        Dispatcher.UIThread.RunJobs();

        Press(window, PhysicalKey.Enter);
        Assert.Equal(2, editor.CurrentFindMatch);
        Assert.Equal(4, editor.SelectionStart);

        Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
        Press(window, PhysicalKey.Enter, RawInputModifiers.Shift);
        Assert.Equal(3, editor.CurrentFindMatch);

        Press(window, PhysicalKey.F3);
        Assert.Equal(1, editor.CurrentFindMatch);
        Assert.Equal("1/3", editor.FindBarControl!.CountText);
    });

    [Fact(DisplayName = "Find starts at the first match at or after the selection")]
    public Task Find_StartsAtSelection() => session.RunAsync(() =>
    {
        var (editor, _) = Show("a x a x a");
        editor.CaretOffset = 3;

        Assert.Equal(3, editor.Find("a"));
        Assert.Equal(2, editor.CurrentFindMatch);
        Assert.Equal(4, editor.SelectionStart);
    });

    [Fact(DisplayName = "Every match is highlighted apart from the host's marks, the current one in its own brush")]
    public Task Find_HighlightsMatchesApartFromMarks() => session.RunAsync(() =>
    {
        var (editor, _) = Show("a x a");
        editor.AddMark(2, 1, MarkdownRangeMarkStyle.Background, Brushes.Red);

        editor.Find("a");

        Assert.Equal([0, 4], editor.FindHighlights.Select(mark => mark.Start));
        Assert.Same(editor.FindCurrentMatchBrush, editor.FindHighlights[0].Brush);
        Assert.Same(editor.FindMatchBrush, editor.FindHighlights[1].Brush);
        Assert.Single(editor.Marks);

        editor.ClearMarks();

        Assert.Equal(2, editor.FindHighlights.Count);
    });

    [Fact(DisplayName = "The highlights follow edits, and CloseFind takes them away and keeps the selection on the match")]
    public Task Edits_FindAgain_CloseClears() => session.RunAsync(() =>
    {
        var (editor, _) = Show("a x a");
        editor.Find("a");

        editor.Replace(2, 1, "a");

        Assert.Equal(3, editor.FindMatchCount);
        Assert.Equal(1, editor.CurrentFindMatch);

        editor.Select(2, 0);
        editor.Replace(5, 0, "!");

        Assert.Equal(3, editor.FindMatchCount);
        Assert.Equal(0, editor.CurrentFindMatch);
        editor.Select(0, 1);

        editor.CloseFind();

        Assert.Equal(0, editor.FindMatchCount);
        Assert.Empty(editor.FindHighlights);
        Assert.Equal("a", editor.SelectedText);
    });

    [Fact(DisplayName = "Setting Markdown finds again in the new text")]
    public Task SetMarkdown_FindsAgain() => session.RunAsync(() =>
    {
        var (editor, _) = Show("a");
        editor.Find("a");

        editor.Markdown = "a a a";

        Assert.Equal(3, editor.FindMatchCount);
    });

    [Fact(DisplayName = "Whole word and match case narrow the matches, from code and from the bar's buttons")]
    public Task Options_NarrowMatches() => session.RunAsync(() =>
    {
        var (editor, _) = Show("Cat cat concat");

        Assert.Equal(3, editor.Find("cat"));
        Assert.Equal(2, editor.Find("cat", MarkdownFindOptions.WholeWord));
        Assert.Equal(1, editor.Find("cat", MarkdownFindOptions.WholeWord | MarkdownFindOptions.MatchCase));

        editor.OpenFind();
        var bar = editor.FindBarControl!;
        Assert.True(bar.WholeWord!.IsChecked);
        Assert.True(bar.MatchCase.IsChecked);

        bar.MatchCase.IsChecked = false;

        Assert.Equal(2, editor.FindMatchCount);
    });

    [Fact(DisplayName = "An invalid regular expression typed in the bar marks the field and finds nothing; from code it throws")]
    public Task InvalidRegularExpression() => session.RunAsync(() =>
    {
        var (editor, _) = Show("(a) b");
        editor.OpenFind();
        var bar = editor.FindBarControl!;
        bar.RegularExpression!.IsChecked = true;

        bar.Field.Text = "(a";
        Dispatcher.UIThread.RunJobs();

        Assert.True(bar.ShowsInvalid);
        Assert.Equal(0, editor.FindMatchCount);

        bar.Field.Text = @"\(a\)";
        Dispatcher.UIThread.RunJobs();

        Assert.False(bar.ShowsInvalid);
        Assert.Equal(1, editor.FindMatchCount);
        Assert.ThrowsAny<ArgumentException>(() => editor.Find("(a", MarkdownFindOptions.RegularExpression));
    });

    [Fact(DisplayName = "ReplaceCurrentMatch replaces the current match as one undo step and selects the next")]
    public Task ReplaceCurrentMatch_ReplacesAndMovesOn() => session.RunAsync(() =>
    {
        var (editor, _) = Show("cat cat cat");
        editor.Find("cat");

        Assert.True(editor.ReplaceCurrentMatch("cats"));

        // The next match is found after the replacement, which has the text to find in it too.
        Assert.Equal("cats cat cat", editor.Markdown);
        Assert.Equal(3, editor.FindMatchCount);
        Assert.Equal(2, editor.CurrentFindMatch);
        Assert.Equal((5, 3), (editor.SelectionStart, editor.SelectionLength));

        Assert.True(editor.Undo());
        Assert.Equal("cat cat cat", editor.Markdown);
    });

    [Fact(DisplayName = "ReplaceCurrentMatch with no current match selects the next one and replaces nothing")]
    public Task ReplaceCurrentMatch_NoCurrent_SelectsNext() => session.RunAsync(() =>
    {
        var (editor, _) = Show("cat x cat");
        editor.Find("cat");
        editor.Select(5, 0);
        Assert.Equal(0, editor.CurrentFindMatch);

        Assert.False(editor.ReplaceCurrentMatch("dog"));

        Assert.Equal("cat x cat", editor.Markdown);
        Assert.Equal(2, editor.CurrentFindMatch);
    });

    [Fact(DisplayName = "ReplaceAllMatches replaces every match, groups filled in, as one undo step")]
    public Task ReplaceAllMatches_OneUndoStep() => session.RunAsync(() =>
    {
        var (editor, _) = Show("[a](x.md) and [b](y.md)");
        editor.Find(@"\[(\w)\]\(([^)]+)\)", MarkdownFindOptions.RegularExpression);

        Assert.Equal(2, editor.ReplaceAllMatches("<$2 $1>"));

        Assert.Equal("<x.md a> and <y.md b>", editor.Markdown);
        Assert.Equal(0, editor.FindMatchCount);

        Assert.True(editor.Undo());
        Assert.Equal("[a](x.md) and [b](y.md)", editor.Markdown);
        Assert.Equal(2, editor.FindMatchCount);
    });

    [Fact(DisplayName = "Nothing is replaced in a read-only editor")]
    public Task ReadOnly_ReplacesNothing() => session.RunAsync(() =>
    {
        var (editor, _) = Show("cat");
        editor.IsReadOnly = true;
        editor.Find("cat");

        Assert.False(editor.ReplaceCurrentMatch("dog"));
        Assert.Equal(0, editor.ReplaceAllMatches("dog"));
        Assert.Equal("cat", editor.Markdown);
    });

    [Fact(DisplayName = "Enter in the replace field replaces the current match, and with the command modifier every match")]
    public Task ReplaceField_Enter() => session.RunAsync(() =>
    {
        var (editor, window) = Show("a a a");
        editor.OpenReplace("a");
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.FindBarControl!.ReplaceField!.IsFocused);
        window.KeyTextInput("b");
        Dispatcher.UIThread.RunJobs();

        Press(window, PhysicalKey.Enter);

        Assert.Equal("b a a", editor.Markdown);

        Press(window, PhysicalKey.Enter, Command);

        Assert.Equal("b b b", editor.Markdown);
    });

    [Fact(DisplayName = "Escape closes the bar and gives the focus back to the text")]
    public Task Escape_ClosesAndFocusesText() => session.RunAsync(() =>
    {
        var (editor, window) = Show("a a");
        FocusText(editor);
        Press(window, PhysicalKey.F, Command);
        window.KeyTextInput("a");
        Dispatcher.UIThread.RunJobs();

        Press(window, PhysicalKey.Escape);

        Assert.False(editor.IsFindOpen);
        Assert.True(editor.TextEditor.TextArea.IsFocused);
        Assert.Empty(editor.FindHighlights);
    });

    [Fact(DisplayName = "Opening the bar again shows the last find's matches again")]
    public Task Reopen_FindsAgain() => session.RunAsync(() =>
    {
        var (editor, _) = Show("a a");
        editor.OpenFind("a");
        editor.CloseFind();

        editor.OpenFind();

        Assert.Equal(2, editor.FindMatchCount);
    });

    [Fact(DisplayName = "The current match is scrolled into view, clear of the find bar")]
    public Task Find_ScrollsMatchIntoView() => session.RunAsync(() =>
    {
        var text = string.Concat(Enumerable.Repeat("line\n", 200)) + "needle";
        var (editor, _) = Show(text, height: 300);
        editor.OpenFind("needle");
        Dispatcher.UIThread.RunJobs();

        var bounds = editor.GetTextBounds(text.Length - 6, 6);

        Assert.NotNull(bounds);
        var bar = editor.FindBarControl!.Bounds;
        Assert.True(bounds.Value.Top >= bar.Bottom, $"The match at {bounds.Value} is under the bar at {bar}.");
        Assert.True(bounds.Value.Bottom <= editor.Bounds.Height, $"The match at {bounds.Value} is below the view.");
    });

    /// <summary>A first line whose last word sits where the find bar does, at the right, above many short lines.</summary>
    private static readonly string s_textWithNeedleOnFirstLine =
        string.Concat(Enumerable.Repeat("word ", 14)) + "needle\n" + string.Concat(Enumerable.Repeat("line\n", 200)) + "end";

    private static int FirstLineNeedle => s_textWithNeedleOnFirstLine.IndexOf("needle", StringComparison.Ordinal);

    [Theory(DisplayName = "A match on the first line, which cannot scroll out from under the bar, moves the bar to the bottom of the text")]
    [InlineData(false)]
    [InlineData(true)]
    public Task Find_MatchOnFirstLine_MovesBarOutOfTheWay(bool replace) => session.RunAsync(() =>
    {
        var (editor, _) = Show(s_textWithNeedleOnFirstLine, height: 300);
        if (replace)
        {
            editor.OpenReplace("needle");
        }
        else
        {
            editor.OpenFind("needle");
        }
        Dispatcher.UIThread.RunJobs();

        var bounds = editor.GetTextBounds(FirstLineNeedle, 6);

        Assert.NotNull(bounds);
        Assert.Equal(replace, editor.IsReplaceOpen);
        var bar = editor.FindBarControl!.Bounds;
        Assert.False(bounds.Value.Intersects(bar), $"The match at {bounds.Value} is under the bar at {bar}.");
        Assert.True(bar.Top > bounds.Value.Bottom, $"The bar at {bar} did not move below the match at {bounds.Value}.");
        Assert.True(bar.Bottom <= editor.Bounds.Height, $"The bar at {bar} is below the editor.");
    });

    /// <summary>The first line's match, one in the middle, and one at the very end, each at the right of its line.</summary>
    private static string TextWithThreeNeedles()
    {
        var words = string.Concat(Enumerable.Repeat("word ", 14));
        var lines = string.Concat(Enumerable.Repeat("line\n", 100));
        return words + "needle\n" + lines + words + "needle\n" + lines + words + "needle";
    }

    [Fact(DisplayName = "The bar moved to the bottom stays there while the next matches scroll clear of it")]
    public Task Find_BarMovedToBottom_StaysForLaterMatches() => session.RunAsync(() =>
    {
        var text = TextWithThreeNeedles();
        var (editor, _) = Show(text, height: 300);
        editor.OpenFind("needle");
        Dispatcher.UIThread.RunJobs();
        var middle = editor.Bounds.Height / 2;
        Assert.True(editor.FindBarControl!.Bounds.Top > middle);

        editor.FindNext();
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.FindBarControl.Bounds.Top > middle, "The bar went back to the top for the match in the middle.");

        editor.FindNext();
        Dispatcher.UIThread.RunJobs();

        var last = editor.GetTextBounds(text.Length - 6, 6);
        var bar = editor.FindBarControl.Bounds;
        Assert.NotNull(last);
        Assert.False(last.Value.Intersects(bar), $"The last match at {last.Value} is under the bar at {bar}.");
        Assert.True(bar.Top > middle, "The bar went back to the top for the match at the end.");
    });

    [Fact(DisplayName = "Without scrolling below the text, a match at the end under the bar at the bottom moves it back to the top")]
    public Task Find_MatchAtEndUnderBarAtBottom_MovesBarBack() => session.RunAsync(() =>
    {
        var text = TextWithThreeNeedles();
        var (editor, _) = Show(text, height: 300);
        editor.TextEditor.Options.AllowScrollBelowDocument = false;
        Dispatcher.UIThread.RunJobs();
        editor.OpenFind("needle");
        Dispatcher.UIThread.RunJobs();
        var middle = editor.Bounds.Height / 2;
        Assert.True(editor.FindBarControl!.Bounds.Top > middle);

        editor.FindPrevious();
        Dispatcher.UIThread.RunJobs();

        var last = editor.GetTextBounds(text.Length - 6, 6);
        var bar = editor.FindBarControl.Bounds;
        Assert.NotNull(last);
        Assert.False(last.Value.Intersects(bar), $"The last match at {last.Value} is under the bar at {bar}.");
        Assert.True(bar.Top < middle, $"The bar at {bar} did not go back to the top for the match at the end at {last.Value}.");
    });

    [Fact(DisplayName = "A match on the first line clear of the bar leaves the bar at the top")]
    public Task Find_MatchOnFirstLineClearOfBar_LeavesBar() => session.RunAsync(() =>
    {
        var text = "needle\n" + string.Concat(Enumerable.Repeat("line\n", 200));
        var (editor, _) = Show(text, height: 300);
        editor.OpenFind("needle");
        Dispatcher.UIThread.RunJobs();

        var bounds = editor.GetTextBounds(0, 6);
        var bar = editor.FindBarControl!.Bounds;

        Assert.NotNull(bounds);
        Assert.False(bounds.Value.Intersects(bar), $"The match at {bounds.Value} is under the bar at {bar}.");
        Assert.True(bar.Top < bounds.Value.Bottom, $"The bar at {bar} moved although the match at {bounds.Value} is clear of it.");
    });

    [Fact(DisplayName = "Closing the find bar puts it back at the top for the next time it opens")]
    public Task CloseFind_PutsBarBackAtTop() => session.RunAsync(() =>
    {
        var (editor, _) = Show(s_textWithNeedleOnFirstLine, height: 300);
        editor.OpenFind("needle");
        Dispatcher.UIThread.RunJobs();
        Assert.True(editor.FindBarControl!.Bounds.Top > editor.Bounds.Height / 2);

        editor.CloseFind();
        editor.OpenFind("end");
        Dispatcher.UIThread.RunJobs();

        Assert.True(editor.FindBarControl.Bounds.Top < editor.Bounds.Height / 2);
    });

    [Fact(DisplayName = "The bar's labels are English by default and the host can replace them")]
    public Task Labels_CanBeReplaced() => session.RunAsync(() =>
    {
        var (editor, _) = Show("a");
        editor.OpenReplace();
        var bar = editor.FindBarControl!;

        Assert.Equal("Find", bar.Field.PlaceholderText);
        Assert.Equal("Replace", bar.ReplaceField!.PlaceholderText);
        Assert.Equal("Match whole word", AutomationProperties.GetName(bar.WholeWord!));

        editor.FindBarLabels = item => item == MarkdownFindBarItem.ReplaceField ? "取代為" : null;

        Assert.Equal("取代為", bar.ReplaceField.PlaceholderText);
        Assert.Equal("Find", bar.Field.PlaceholderText);
    });

    [Fact(DisplayName = "The viewer's find bar has no editor-only parts")]
    public Task ViewerBar_HasNoReplaceParts() => session.RunAsync(() =>
    {
        var viewer = new MarkdownViewer { Markdown = "a" };
        var window = new Window { Width = 600, Height = 400, Content = viewer };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        viewer.OpenFind();

        Assert.Null(viewer.FindBarControl!.ReplaceField);
        Assert.Null(viewer.FindBarControl.WholeWord);
        Assert.Null(viewer.FindBarControl.RegularExpression);
    });
}
