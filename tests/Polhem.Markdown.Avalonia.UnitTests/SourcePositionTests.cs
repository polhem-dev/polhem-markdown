using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>Positions in the source and on screen, which a host uses to keep an editor and a viewer in step.</summary>
[Collection(HeadlessCollection.Name)]
public class SourcePositionTests(HeadlessSession session)
{
    private const string Text = "# Title\n\nThe first paragraph.\n\n- one\n- two\n\nThe last paragraph.";

    private static RawInputModifiers CommandKey =>
        (Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control) == KeyModifiers.Meta
            ? RawInputModifiers.Meta
            : RawInputModifiers.Control;

    [Fact(DisplayName = "The start of each block's source shows at the top of that block, and back again")]
    public Task BlockStarts_MapBothWays() => session.RunAsync(() =>
    {
        var (_, viewer) = Show(Text);
        var blocks = Blocks(viewer).ToList();
        string[] starts = ["# Title", "The first", "- one", "The last"];

        for (var index = 0; index < starts.Length; index++)
        {
            var offset = Text.IndexOf(starts[index], StringComparison.Ordinal);
            Assert.Equal(blocks[index].Bounds.Top, viewer.GetVerticalOffset(offset));
            Assert.Equal(offset, viewer.GetSourceOffset(blocks[index].Bounds.Top));
        }
    });

    [Fact(DisplayName = "Further into the source is further down, within a block too")]
    public Task Positions_Increase() => session.RunAsync(() =>
    {
        var (_, viewer) = Show(Text);

        var positions = Enumerable.Range(0, Text.Length + 1).Select(offset => viewer.GetVerticalOffset(offset)!.Value).ToList();

        Assert.Equal(positions.Order(), positions);
        var list = Blocks(viewer).ElementAt(2).Bounds;
        var inList = viewer.GetVerticalOffset(Text.IndexOf("two", StringComparison.Ordinal))!.Value;
        Assert.InRange(inList, list.Top + 1, list.Bottom);
    });

    [Fact(DisplayName = "Above the first block is its start, below the last block is its end")]
    public Task OutsideBlocks_ClampToEnds() => session.RunAsync(() =>
    {
        var (_, viewer) = Show(Text);

        Assert.Equal(0, viewer.GetSourceOffset(-50));
        Assert.Equal(Text.Length, viewer.GetSourceOffset(viewer.Bounds.Height + 50));
    });

    [Fact(DisplayName = "With nothing shown there is no position")]
    public Task NothingShown_GivesNull() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("");

        Assert.Null(viewer.GetVerticalOffset(0));
        Assert.Null(viewer.GetSourceOffset(0));
    });

    [Fact(DisplayName = "After a change only some blocks rebuild, and the kept ones follow their source's new place")]
    public Task PartialRebuild_KeepsRangesCurrent() => session.RunAsync(() =>
    {
        var (_, viewer) = Show(Text);
        var changed = "# A new title\n\n" + Text;

        viewer.Markdown = changed;
        Dispatcher.UIThread.RunJobs();

        var last = changed.IndexOf("The last", StringComparison.Ordinal);
        Assert.Equal(Blocks(viewer).Last().Bounds.Top, viewer.GetVerticalOffset(last));
    });

    [Fact(DisplayName = "A click with the command key asks for the source at that place, and a handler takes the click")]
    public Task CommandClick_RaisesSourceRequested() => session.RunAsync(() =>
    {
        var (window, viewer) = Show(Text);
        var requested = new List<int>();
        viewer.SourceRequested += (_, e) =>
        {
            requested.Add(e.SourceOffset);
            e.Handled = true;
        };
        // NOTE: a click lands on a text block only where it draws, and where its first glyphs fall depends on the fonts
        // the machine has, so the click is in the middle of the line rather than at its edge.
        var last = Blocks(viewer).Last();
        var point = last.TranslatePoint(new Point(10, last.Bounds.Height / 2), window)!.Value;

        window.MouseDown(point, MouseButton.Left, CommandKey);
        window.MouseUp(point, MouseButton.Left, CommandKey);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);

        var offset = Assert.Single(requested);
        Assert.InRange(offset, Text.IndexOf("The last", StringComparison.Ordinal), Text.Length);
    });

    [Fact(DisplayName = "The editor reports the source at the top of its view, and scrolls to a source offset")]
    public Task Editor_ScrollsBySource() => session.RunAsync(() =>
    {
        var text = string.Concat(Enumerable.Range(1, 200).Select(line => $"Line {line}\n"));
        var editor = new MarkdownEditor { Markdown = text, WordWrap = false };
        var window = new Window { Width = 400, Height = 300, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, editor.FirstVisibleSourceOffset);

        var target = text.IndexOf("Line 120", StringComparison.Ordinal);
        editor.ScrollToSourceOffset(target);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(target, editor.FirstVisibleSourceOffset);
    });

    [Fact(DisplayName = "Scrolling the editor into a line reports a source offset inside that line")]
    public Task Editor_MidLine() => session.RunAsync(() =>
    {
        var text = string.Concat(Enumerable.Range(1, 200).Select(line => $"Line {line:000}\n"));
        var editor = new MarkdownEditor { Markdown = text, WordWrap = false };
        var window = new Window { Width = 400, Height = 300, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var lineStart = text.IndexOf("Line 050", StringComparison.Ordinal);
        editor.ScrollToSourceOffset(lineStart + 4);
        Dispatcher.UIThread.RunJobs();

        Assert.InRange(editor.FirstVisibleSourceOffset, lineStart + 3, lineStart + 5);
    });
}
