using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>A selection that runs from one block into others, as in a browser (polhem-dev/polhem-markdown#1).</summary>
[Collection(HeadlessCollection.Name)]
public class SelectionTests(HeadlessSession session)
{
    private const string ThreeParagraphs = "First paragraph here.\n\nSecond paragraph here.\n\nThird paragraph here.";

    private static RawInputModifiers CommandKey =>
        (Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control) == KeyModifiers.Meta
            ? RawInputModifiers.Meta
            : RawInputModifiers.Control;

    [Fact(DisplayName = "A drag from one paragraph into another selects the end of the first, the blocks between, and the start of the last")]
    public Task Drag_AcrossBlocks_SelectsBetween() => session.RunAsync(() =>
    {
        var (window, viewer) = Show(ThreeParagraphs);
        var texts = Texts(viewer);

        Drag(window, Near(texts[0], window), Near(texts[2], window));

        var (firstStart, firstEnd) = Range(texts[0]);
        Assert.InRange(firstStart, 1, Length(texts[0]) - 1);
        Assert.Equal(Length(texts[0]), firstEnd);
        Assert.Equal((0, Length(texts[1])), Range(texts[1]));
        var (lastStart, lastEnd) = Range(texts[2]);
        Assert.Equal(0, lastStart);
        Assert.InRange(lastEnd, 1, Length(texts[2]) - 1);
        Assert.Equal(
            texts[0].SelectedText + "\n\nSecond paragraph here.\n\n" + texts[2].SelectedText,
            viewer.SelectedText);
    });

    [Fact(DisplayName = "A drag back up selects from the start of the block it reaches to where it started")]
    public Task Drag_Backwards_SelectsBetween() => session.RunAsync(() =>
    {
        var (window, viewer) = Show(ThreeParagraphs);
        var texts = Texts(viewer);

        Drag(window, Near(texts[2], window), Near(texts[0], window));

        Assert.Equal(Length(texts[0]), Range(texts[0]).End);
        Assert.InRange(Range(texts[0]).Start, 1, Length(texts[0]) - 1);
        Assert.Equal((0, Length(texts[1])), Range(texts[1]));
        Assert.Equal(0, Range(texts[2]).Start);
        Assert.InRange(Range(texts[2]).End, 1, Length(texts[2]) - 1);
    });

    [Fact(DisplayName = "A drag that stays inside one block selects inside that block only")]
    public Task Drag_InsideOneBlock_StaysInIt() => session.RunAsync(() =>
    {
        var (window, viewer) = Show(ThreeParagraphs);
        var texts = Texts(viewer);
        var start = Near(texts[1], window);

        Drag(window, start, start + new Point(80, 0));

        Assert.NotEqual(texts[1].SelectionStart, texts[1].SelectionEnd);
        Assert.Equal(texts[1].SelectedText, viewer.SelectedText);
        Assert.False(viewer.SpansTexts);
        Assert.All([texts[0], texts[2]], text => Assert.Equal(text.SelectionStart, text.SelectionEnd));
    });

    [Fact(DisplayName = "Copied text puts a line break between list items and table rows, a tab between cells, and an empty line between other blocks")]
    public Task SelectedText_SeparatesBlocksAsABrowserDoes() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("Intro\n\n- one\n- two\n\n| a | b |\n|---|---|\n| c | d |\n\n```\nx = 1\n```\n\nEnd");

        viewer.SelectAll();

        Assert.Equal("Intro\n\none\ntwo\n\na\tb\nc\td\n\nx = 1\n\nEnd", viewer.SelectedText);
    });

    [Fact(DisplayName = "The command key with A selects the whole document, and with C copies it")]
    public Task Keys_SelectAllAndCopy() => session.RunAsync(async () =>
    {
        var (window, viewer) = Show(ThreeParagraphs);
        var texts = Texts(viewer);
        var point = Near(texts[1], window);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);

        window.KeyPressQwerty(PhysicalKey.A, CommandKey);
        window.KeyPressQwerty(PhysicalKey.C, CommandKey);
        Dispatcher.UIThread.RunJobs();

        Assert.All(texts, text => Assert.Equal((0, Length(text)), Range(text)));
        Assert.Equal(
            "First paragraph here.\n\nSecond paragraph here.\n\nThird paragraph here.",
            await window.Clipboard!.TryGetTextAsync());
    });

    [Fact(DisplayName = "A text's own copy command copies the whole selection when it spans blocks")]
    public Task TextCopy_CopiesWholeSelection() => session.RunAsync(async () =>
    {
        var (window, viewer) = Show(ThreeParagraphs);
        viewer.SelectAll();

        Texts(viewer)[1].Copy();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(viewer.SelectedText, await window.Clipboard!.TryGetTextAsync());
    });

    [Fact(DisplayName = "A click clears a selection that spans blocks")]
    public Task Click_ClearsSelection() => session.RunAsync(() =>
    {
        var (window, viewer) = Show(ThreeParagraphs);
        var texts = Texts(viewer);
        viewer.SelectAll();

        var point = Near(texts[1], window);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);

        Assert.Equal("", viewer.SelectedText);
    });

    [Fact(DisplayName = "A right click inside the selection keeps it, for the context menu's copy")]
    public Task RightClick_InsideSelection_KeepsIt() => session.RunAsync(() =>
    {
        var (window, viewer) = Show(ThreeParagraphs);
        var texts = Texts(viewer);
        viewer.SelectAll();
        var before = viewer.SelectedText;

        var point = Near(texts[1], window);
        window.MouseDown(point, MouseButton.Right);
        window.MouseUp(point, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(before, viewer.SelectedText);
    });

    [Fact(DisplayName = "A shift-click in another block extends the selection to it")]
    public Task ShiftClick_ExtendsIntoAnotherBlock() => session.RunAsync(() =>
    {
        var (window, viewer) = Show(ThreeParagraphs);
        var texts = Texts(viewer);
        var start = Near(texts[0], window);
        window.MouseDown(start, MouseButton.Left);
        window.MouseUp(start, MouseButton.Left);

        var end = Near(texts[2], window);
        window.MouseDown(end, MouseButton.Left, RawInputModifiers.Shift);
        window.MouseUp(end, MouseButton.Left, RawInputModifiers.Shift);

        Assert.True(viewer.SpansTexts);
        Assert.Equal((0, Length(texts[1])), Range(texts[1]));
        Assert.Equal(0, Range(texts[2]).Start);
    });

    [Fact(DisplayName = "A selection survives a reply growing after it, and is cleared when a block it ends in is rebuilt")]
    public Task Update_KeepsSelectionInKeptBlocks() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("one\n\ntwo\n\nthr");
        var texts = Texts(viewer);
        Drag(viewer, texts[0], texts[1]);

        viewer.Markdown = "one\n\ntwo\n\nthree";
        Assert.Equal("one\n\ntwo", viewer.SelectedText);

        viewer.Markdown = "one\n\ntwo!\n\nthree";
        Assert.Equal("", viewer.SelectedText);
    });

    [Fact(DisplayName = "The text of a closed section is left out of the selection")]
    public Task SelectAll_SkipsClosedSection() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("before\n\n<details><summary>More</summary>\n\nhidden\n\n</details>\n\nafter");

        viewer.SelectAll();

        Assert.DoesNotContain("hidden", viewer.SelectedText, StringComparison.Ordinal);
        Assert.Contains("after", viewer.SelectedText, StringComparison.Ordinal);
    });

    [Fact(DisplayName = "Moving the focus out of the viewer clears a selection that spans blocks")]
    public Task FocusLeaves_ClearsSelection() => session.RunAsync(() =>
    {
        var viewer = new MarkdownViewer { ImageLoader = NoImages, Markdown = ThreeParagraphs };
        var button = new Button { Content = "Elsewhere" };
        var window = new Window { Width = 600, Height = 800, Content = new StackPanel { Children = { viewer, button } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var texts = Texts(viewer);
        var point = Near(texts[0], window);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        viewer.SelectAll();

        button.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("", viewer.SelectedText);
    });

    [Fact(DisplayName = "Moving the focus out of the viewer clears the selection also when the focused text holds none of it")]
    public Task FocusLeaves_FromTextWithoutSelection_ClearsSelection() => session.RunAsync(() =>
    {
        var viewer = new MarkdownViewer { ImageLoader = NoImages, Markdown = ThreeParagraphs };
        var button = new Button { Content = "Elsewhere" };
        var window = new Window { Width = 600, Height = 800, Content = new StackPanel { Children = { viewer, button } } };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var texts = Texts(viewer);
        var start = texts[0].TranslatePoint(new Point(texts[0].Bounds.Width - 1, texts[0].Bounds.Height / 2), window)!.Value;

        Drag(window, start, Near(texts[2], window));
        Assert.Equal(texts[0].SelectionStart, texts[0].SelectionEnd);
        Assert.True(viewer.SpansTexts);

        button.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("", viewer.SelectedText);
    });

    [Fact(DisplayName = "A drag below the scroll viewer scrolls it, and the selection follows into the text scrolled in")]
    public Task Drag_BelowScrollViewer_Scrolls() => session.RunAsync(() =>
    {
        var text = string.Join("\n\n", Enumerable.Range(1, 60).Select(index => $"Paragraph {index} of the text."));
        var viewer = new MarkdownViewer { ImageLoader = NoImages, Markdown = text };
        var scroller = new ScrollViewer { Content = viewer };
        var window = new Window { Width = 600, Height = 300, Content = scroller };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var texts = Texts(viewer);

        var start = Near(texts[0], window);
        window.MouseDown(start, MouseButton.Left);
        window.MouseMove(new Point(start.X, 299), RawInputModifiers.LeftMouseButton);
        window.MouseMove(new Point(start.X, 340), RawInputModifiers.LeftMouseButton);
        var selectedBefore = texts.Count(text => text.SelectionStart != text.SelectionEnd);
        for (var step = 0; step < 10; step++)
        {
            viewer.ScrollStep();
        }
        var selectedAfter = texts.Count(text => text.SelectionStart != text.SelectionEnd);
        window.MouseUp(new Point(start.X, 340), MouseButton.Left);

        Assert.True(scroller.Offset.Y > 0);
        Assert.True(selectedAfter > selectedBefore, $"{selectedAfter} texts selected after scrolling, {selectedBefore} before");
    });

    private static List<MarkdownTextBlock> Texts(MarkdownViewer viewer) => Descendants<MarkdownTextBlock>(viewer).ToList();

    /// <summary>
    /// A point a little way into the first line of a text, in the window's coordinates.
    /// </summary>
    /// <remarks>
    /// NOTE: where the first glyphs fall depends on the fonts the machine has, so the point is a few characters in
    /// rather than at the edge.
    /// </remarks>
    private static Point Near(MarkdownTextBlock text, Window window) =>
        text.TranslatePoint(new Point(30, text.Bounds.Height / 2), window)!.Value;

    private static void Drag(Window window, Point from, Point to)
    {
        window.MouseDown(from, MouseButton.Left);
        window.MouseMove(from + new Point(2, 0), RawInputModifiers.LeftMouseButton);
        window.MouseMove(to, RawInputModifiers.LeftMouseButton);
        window.MouseUp(to, MouseButton.Left);
    }

    /// <summary>Selects from the start of <paramref name="from"/> to the end of <paramref name="to"/>, as a drag would.</summary>
    private static void Drag(MarkdownViewer viewer, MarkdownTextBlock from, MarkdownTextBlock to)
    {
        viewer.ClearSelection();
        var window = (Window)TopLevel.GetTopLevel(viewer)!;
        var start = from.TranslatePoint(new Point(0, from.Bounds.Height / 2), window)!.Value;
        var end = to.TranslatePoint(new Point(to.Bounds.Width - 1, to.Bounds.Height / 2), window)!.Value;
        Drag(window, start, end);
    }

    private static (int Start, int End) Range(SelectableTextBlock text) =>
        (Math.Min(text.SelectionStart, text.SelectionEnd), Math.Max(text.SelectionStart, text.SelectionEnd));

    private static int Length(MarkdownTextBlock text) => (text.Inlines is { Count: > 0 } inlines ? inlines.Text : text.Text)?.Length ?? 0;
}
