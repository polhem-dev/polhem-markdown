using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class MarkdownEditorMarkTests(HeadlessSession session)
{
    private static readonly IBrush s_red = new SolidColorBrush(Colors.Red);

    private static (MarkdownEditor Editor, Window Window) Show(string markdown)
    {
        var editor = new MarkdownEditor { Markdown = markdown };
        var window = new Window { Width = 600, Height = 400, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (editor, window);
    }

    private static void Type(MarkdownEditor editor, int offset, string text)
    {
        editor.CaretOffset = offset;
        var area = editor.TextEditor.TextArea;
        foreach (var character in text)
        {
            area.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = character.ToString() });
        }
    }

    private static string MarkedText(MarkdownEditor editor, MarkdownRangeMark mark) => editor.Markdown!.Substring(mark.Start, mark.Length);

    [Fact(DisplayName = "AddMark keeps the range, style, brush and owner, and lists the mark")]
    public Task AddMark_KeepsWhatItWasGiven() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");

        var mark = editor.AddMark(4, 3, MarkdownRangeMarkStyle.Strikethrough, s_red, "diff");

        Assert.Equal(4, mark.Start);
        Assert.Equal(3, mark.Length);
        Assert.Equal(MarkdownRangeMarkStyle.Strikethrough, mark.Style);
        Assert.Same(s_red, mark.Brush);
        Assert.Equal("diff", mark.Owner);
        Assert.False(mark.IsRemoved);
        Assert.Equal([mark], editor.Marks);
    });

    [Fact(DisplayName = "Marks lists the marks in the order their ranges start")]
    public Task Marks_InDocumentOrder() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");

        var three = editor.AddMark(8, 5, MarkdownRangeMarkStyle.Background, s_red);
        var one = editor.AddMark(0, 3, MarkdownRangeMarkStyle.Underline, s_red);

        Assert.Equal([one, three], editor.Marks);
    });

    [Fact(DisplayName = "AddMark rejects an empty range, a range outside the text, a null brush and an undefined style")]
    public Task AddMark_InvalidArguments_Throw() => session.RunAsync(() =>
    {
        var (editor, _) = Show("abc");

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.AddMark(1, 0, MarkdownRangeMarkStyle.Background, s_red));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.AddMark(-1, 1, MarkdownRangeMarkStyle.Background, s_red));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.AddMark(2, 2, MarkdownRangeMarkStyle.Background, s_red));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.AddMark(0, 1, (MarkdownRangeMarkStyle)42, s_red));
        Assert.Throws<ArgumentNullException>(() => editor.AddMark(0, 1, MarkdownRangeMarkStyle.Background, null!));
        Assert.Empty(editor.Marks);
    });

    [Fact(DisplayName = "A mark moves with text typed before it, and keeps covering the same text")]
    public Task Mark_FollowsTypingBefore() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");
        var mark = editor.AddMark(4, 3, MarkdownRangeMarkStyle.Background, s_red);

        Type(editor, 0, "zero ");

        Assert.Equal(9, mark.Start);
        Assert.Equal("two", MarkedText(editor, mark));
    });

    [Fact(DisplayName = "Text typed inside a mark is part of it; text typed where it starts or ends is not")]
    public Task Mark_GrowsInsideOnly() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");
        var mark = editor.AddMark(4, 3, MarkdownRangeMarkStyle.Background, s_red);

        Type(editor, 5, "XX");
        Assert.Equal("tXXwo", MarkedText(editor, mark));

        Type(editor, mark.Start, "<");
        Type(editor, mark.Start + mark.Length, ">");
        Assert.Equal("one <tXXwo> three", editor.Markdown);
        Assert.Equal("tXXwo", MarkedText(editor, mark));
    });

    [Fact(DisplayName = "Deleting part of a mark's text shrinks it to the rest")]
    public Task Mark_ShrinksWithPartialDeletion() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");
        var mark = editor.AddMark(4, 9, MarkdownRangeMarkStyle.Background, s_red);

        editor.Replace(2, 4, "");

        Assert.Equal("ono three", editor.Markdown);
        Assert.Equal("o three", MarkedText(editor, mark));
        Assert.False(mark.IsRemoved);
    });

    [Fact(DisplayName = "A mark whose text is all deleted is removed, keeps where it was, and undo does not bring it back")]
    public Task Mark_RemovedWithItsText() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");
        var mark = editor.AddMark(4, 3, MarkdownRangeMarkStyle.Strikethrough, s_red);

        editor.Replace(3, 5, "");

        Assert.Equal("onethree", editor.Markdown);
        Assert.True(mark.IsRemoved);
        Assert.Empty(editor.Marks);
        Assert.Equal(3, mark.Start);
        Assert.Equal(0, mark.Length);

        Assert.True(editor.Undo());
        Assert.Equal("one two three", editor.Markdown);
        Assert.Empty(editor.Marks);
    });

    [Fact(DisplayName = "Several replacements at once move and remove marks as one change")]
    public Task Mark_FollowsSeveralReplacements() => session.RunAsync(() =>
    {
        var (editor, _) = Show("alpha beta gamma delta");
        var beta = editor.AddMark(6, 4, MarkdownRangeMarkStyle.Strikethrough, s_red);
        var delta = editor.AddMark(17, 5, MarkdownRangeMarkStyle.Background, s_red);

        editor.Replace(
        [
            new MarkdownReplacement(0, 0, "> "),
            new MarkdownReplacement(6, 5, ""),
            new MarkdownReplacement(11, 5, "GAMMA!"),
        ]);

        Assert.Equal("> alpha GAMMA! delta", editor.Markdown);
        Assert.True(beta.IsRemoved);
        Assert.Equal("delta", MarkedText(editor, delta));
        Assert.Equal([delta], editor.Marks);
    });

    [Fact(DisplayName = "Replacing all of a mark's text in one change keeps the mark over the new text")]
    public Task Mark_KeptWhenItsTextIsReplaced() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");
        var mark = editor.AddMark(4, 3, MarkdownRangeMarkStyle.Background, s_red);

        editor.Replace(4, 3, "2");

        Assert.False(mark.IsRemoved);
        Assert.Equal("2", MarkedText(editor, mark));
    });

    [Fact(DisplayName = "Setting Markdown removes every mark")]
    public Task SettingMarkdown_RemovesMarks() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");
        var whole = editor.AddMark(0, 13, MarkdownRangeMarkStyle.Background, s_red);
        var part = editor.AddMark(4, 3, MarkdownRangeMarkStyle.Background, s_red);

        editor.Markdown = "something else entirely";

        Assert.Empty(editor.Marks);
        Assert.True(whole.IsRemoved);
        Assert.True(part.IsRemoved);
    });

    [Fact(DisplayName = "ClearMarks with an owner removes that owner's marks and leaves the others")]
    public Task ClearMarks_ByOwner() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");
        var owner = new object();
        var mine = editor.AddMark(0, 3, MarkdownRangeMarkStyle.Background, s_red, owner);
        var other = editor.AddMark(4, 3, MarkdownRangeMarkStyle.Background, s_red, "other");
        var none = editor.AddMark(8, 5, MarkdownRangeMarkStyle.Background, s_red);

        editor.ClearMarks(owner);

        Assert.True(mine.IsRemoved);
        Assert.Equal([other, none], editor.Marks);

        editor.ClearMarks();
        Assert.Empty(editor.Marks);
        Assert.True(other.IsRemoved);
        Assert.True(none.IsRemoved);
    });

    [Fact(DisplayName = "RemoveMark removes a mark once")]
    public Task RemoveMark_RemovesOnce() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");
        var mark = editor.AddMark(4, 3, MarkdownRangeMarkStyle.Underline, s_red);

        Assert.True(editor.RemoveMark(mark));
        Assert.False(editor.RemoveMark(mark));
        Assert.True(mark.IsRemoved);
        Assert.Empty(editor.Marks);
    });

    [Fact(DisplayName = "A mark from one editor is not removed from another")]
    public Task RemoveMark_OtherEditor_ReturnsFalse() => session.RunAsync(() =>
    {
        var (first, _) = Show("one two three");
        var (second, _) = Show("one two three");
        var mark = first.AddMark(4, 3, MarkdownRangeMarkStyle.Underline, s_red);

        Assert.False(second.RemoveMark(mark));
        Assert.False(mark.IsRemoved);
        Assert.Equal([mark], first.Marks);
    });

    [Fact(DisplayName = "A background mark is drawn in its brush, under the selection")]
    public Task BackgroundMark_DrawnUnderSelection() => session.RunAsync(() =>
    {
        var (editor, window) = Show("one two three");
        editor.SelectionBrush = new SolidColorBrush(Colors.Blue);
        editor.AddMark(4, 3, MarkdownRangeMarkStyle.Background, s_red);
        Dispatcher.UIThread.RunJobs();
        var bounds = editor.GetTextBounds(4, 3)!.Value;

        Assert.Equal(Colors.Red, PixelAt(window, editor, new Point(bounds.Left + 2, bounds.Top + 1)));

        editor.Select(4, 3);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Colors.Blue, PixelAt(window, editor, new Point(bounds.Left + 2, bounds.Top + 1)));
    });

    [Theory(DisplayName = "An underline or strikethrough mark draws a line in its brush across the range")]
    [InlineData(MarkdownRangeMarkStyle.Underline)]
    [InlineData(MarkdownRangeMarkStyle.Strikethrough)]
    public Task LineMark_Drawn(MarkdownRangeMarkStyle style) => session.RunAsync(() =>
    {
        var (editor, window) = Show("one two three");
        editor.AddMark(4, 3, style, s_red);
        Dispatcher.UIThread.RunJobs();
        var bounds = editor.GetTextBounds(4, 3)!.Value;

        var rows = Enumerable.Range((int)bounds.Top, (int)bounds.Height)
            .Where(y => PixelAt(window, editor, new Point(bounds.Left + 1, y)) == Colors.Red)
            .ToList();

        Assert.NotEmpty(rows);
        var middle = bounds.Top + bounds.Height / 2;
        if (style == MarkdownRangeMarkStyle.Underline)
        {
            Assert.All(rows, y => Assert.True(y > middle + 2, $"Row {y} is not near the bottom of {bounds}."));
        }
        else
        {
            Assert.All(rows, y => Assert.InRange(y, middle - 3, middle + 4));
        }

        // Nothing red outside the range on the same line.
        var after = editor.GetTextBounds(8, 5)!.Value;
        Assert.DoesNotContain(rows, y => PixelAt(window, editor, new Point(after.Left + 2, y)) == Colors.Red);
    });

    [Fact(DisplayName = "GetTextBounds gives the box of a range in the editor's coordinates, below the toolbar")]
    public Task GetTextBounds_Range() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three\nfour");

        var two = editor.GetTextBounds(4, 3)!.Value;
        var three = editor.GetTextBounds(8, 5)!.Value;
        var both = editor.GetTextBounds(4, 9)!.Value;
        var origin = editor.TextEditor.TextArea.TextView.TranslatePoint(default, editor)!.Value;

        Assert.True(two.Width > 0 && two.Height > 0);
        Assert.True(two.Top >= origin.Y, $"{two} starts above the text view at {origin}.");
        Assert.True(three.Left > two.Right);
        Assert.Equal(two.Top, three.Top);
        Assert.Equal(two.Left, both.Left, 3);
        Assert.Equal(three.Right, both.Right, 3);
    });

    [Fact(DisplayName = "GetTextBounds over two lines covers both, and with length 0 gives a caret's place")]
    public Task GetTextBounds_LinesAndCaret() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three\nfour");

        var first = editor.GetTextBounds(0, 3)!.Value;
        var second = editor.GetTextBounds(14, 4)!.Value;
        var both = editor.GetTextBounds(0, 18)!.Value;
        var caret = editor.GetTextBounds(4, 0)!.Value;
        var two = editor.GetTextBounds(4, 3)!.Value;

        Assert.True(second.Top >= first.Bottom - 0.5);
        Assert.Equal(first.Top, both.Top, 3);
        Assert.Equal(second.Bottom, both.Bottom, 3);
        Assert.Equal(0, caret.Width);
        Assert.Equal(two.Left, caret.Left, 3);
        Assert.Equal(two.Top, caret.Top, 3);
        Assert.Equal(two.Height, caret.Height, 3);
    });

    [Fact(DisplayName = "GetTextBounds is null for a range out of view and for an editor not shown, and rejects a range outside the text")]
    public Task GetTextBounds_OutOfView_IsNull() => session.RunAsync(() =>
    {
        var (editor, _) = Show(string.Join("\n", Enumerable.Range(1, 200).Select(i => $"line {i}")));
        var far = editor.Markdown!.IndexOf("line 180", StringComparison.Ordinal);

        Assert.Null(editor.GetTextBounds(far, 8));
        Assert.Null(editor.GetTextBounds(far, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.GetTextBounds(editor.Markdown.Length, 1));

        var hidden = new MarkdownEditor { Markdown = "one two" };
        Assert.Null(hidden.GetTextBounds(0, 3));
    });

    [Fact(DisplayName = "GetTextBounds follows a scroll, which raises ViewChanged")]
    public Task ViewChanged_OnScroll() => session.RunAsync(() =>
    {
        var (editor, _) = Show(string.Join("\n", Enumerable.Range(1, 200).Select(i => $"line {i}")));
        var target = editor.Markdown!.IndexOf("line 5\n", StringComparison.Ordinal);
        var before = editor.GetTextBounds(target, 6)!.Value;
        var changes = 0;
        editor.ViewChanged += (_, _) => changes++;

        editor.ScrollToSourceOffset(editor.Markdown.IndexOf("line 3\n", StringComparison.Ordinal));
        Dispatcher.UIThread.RunJobs();

        Assert.True(changes > 0);
        var after = editor.GetTextBounds(target, 6)!.Value;
        Assert.True(after.Top < before.Top, $"{after} did not move up from {before}.");
        Assert.Equal(before.Left, after.Left, 3);
    });

    [Fact(DisplayName = "Hiding the toolbar moves the text up and raises ViewChanged")]
    public Task ViewChanged_OnToolbarHidden() => session.RunAsync(() =>
    {
        var (editor, _) = Show("one two three");
        var before = editor.GetTextBounds(4, 3)!.Value;
        var changes = 0;
        editor.ViewChanged += (_, _) => changes++;

        editor.ShowToolbar = false;
        Dispatcher.UIThread.RunJobs();

        Assert.True(changes > 0);
        var after = editor.GetTextBounds(4, 3)!.Value;
        Assert.True(after.Top < before.Top, $"{after} did not move up from {before}.");
    });

    /// <summary>The colour of the rendered window at <paramref name="point"/> in <paramref name="editor"/>'s coordinates.</summary>
    private static Color PixelAt(Window window, MarkdownEditor editor, Point point)
    {
        var inWindow = editor.TranslatePoint(point, window)!.Value;
        using var frame = window.CaptureRenderedFrame()!;
        using var buffer = frame.Lock();
        var x = (int)inWindow.X;
        var y = (int)inWindow.Y;
        var pixel = Marshal.ReadInt32(buffer.Address, y * buffer.RowBytes + x * 4);
        var low = (byte)(pixel & 0xFF);
        var g = (byte)((pixel >> 8) & 0xFF);
        var high = (byte)((pixel >> 16) & 0xFF);
        var (r, b) = buffer.Format == PixelFormat.Rgba8888 ? (low, high) : (high, low);
        return Color.FromRgb(r, g, b);
    }
}
