using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>Finding text in the viewer: the find bar, the highlights and the API (polhem-dev/polhem-markdown#85).</summary>
[Collection(HeadlessCollection.Name)]
public class FindTests(HeadlessSession session)
{
    private static readonly string s_filler = string.Concat(Enumerable.Repeat("A paragraph that takes up some room.\n\n", 40));

    private static (Window Window, ScrollViewer Scroller, MarkdownViewer Viewer) ShowScrolling(string markdown)
    {
        var viewer = new MarkdownViewer { ImageLoader = NoImages, Markdown = markdown };
        var scroller = new ScrollViewer { Content = viewer };
        var window = new Window { Width = 800, Height = 600, Content = scroller };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, scroller, viewer);
    }

    private static void FocusText(Window window, MarkdownViewer viewer)
    {
        var text = Descendants<MarkdownTextBlock>(viewer).First();
        var point = text.TranslatePoint(new Point(2, text.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Type(Window window, string text)
    {
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact(DisplayName = "Ctrl+F (Cmd+F on macOS) opens the find bar at the top right with the focus in it, and typing finds as it goes")]
    public Task CtrlF_OpensBar_TypingFinds() => session.RunAsync(() =>
    {
        var (window, _, viewer) = ShowScrolling("An order form.\n\nThe Order number.\n\nNothing here.");
        FocusText(window, viewer);

        var command = Application.Current!.PlatformSettings!.HotkeyConfiguration.CommandModifiers == KeyModifiers.Meta
            ? RawInputModifiers.Meta
            : RawInputModifiers.Control;
        window.KeyPressQwerty(PhysicalKey.F, command);
        Dispatcher.UIThread.RunJobs();

        var bar = viewer.FindBarControl!;
        Assert.True(viewer.IsFindOpen);
        Assert.True(bar.Field.IsFocused);
        Assert.Equal(viewer.Bounds.Width - 8, bar.Bounds.Right, 1);
        Assert.Equal(8, bar.Bounds.Y, 1);

        Type(window, "order");
        Assert.Equal(2, viewer.FindMatchCount);
        Assert.Equal(1, viewer.CurrentFindMatch);
        Assert.Equal("1/2", bar.CountText);
    });

    [Fact(DisplayName = "Enter goes to the next match and wraps, Shift+Enter to the previous one, and F3 too")]
    public Task Keys_StepThroughMatches() => session.RunAsync(() =>
    {
        var (window, _, viewer) = ShowScrolling("one\n\none\n\none");
        viewer.OpenFind("one");
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(2, viewer.CurrentFindMatch);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Assert.Equal(1, viewer.CurrentFindMatch);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.Shift);
        Assert.Equal(3, viewer.CurrentFindMatch);
        window.KeyPressQwerty(PhysicalKey.F3, RawInputModifiers.None);
        Assert.Equal(1, viewer.CurrentFindMatch);
    });

    [Fact(DisplayName = "Escape closes the bar, takes the highlights away and gives the focus back")]
    public Task Escape_Closes() => session.RunAsync(() =>
    {
        var (window, _, viewer) = ShowScrolling("An order form.");
        FocusText(window, viewer);
        var text = Descendants<MarkdownTextBlock>(viewer).First();
        viewer.OpenFind("order");
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(viewer.IsFindOpen);
        Assert.Equal(0, viewer.FindMatchCount);
        Assert.True(text.IsFocused);
    });

    [Fact(DisplayName = "A match runs across emphasis and links, finds an image by its alternative text, and never spans two blocks")]
    public Task Matches_AcrossInlines() => session.RunAsync(() =>
    {
        var (_, _, viewer) = ShowScrolling("An or**der** form and [ord](https://example.org)er.\n\n![order chart](https://example.org/c.png)\n\nor\n\nder");

        Assert.Equal(3, viewer.Find("order"));
    });

    [Fact(DisplayName = "Case is ignored unless asked for, from the API and the bar's Aa button")]
    public Task MatchCase() => session.RunAsync(() =>
    {
        var (window, _, viewer) = ShowScrolling("Order order ORDER");

        Assert.Equal(3, viewer.Find("order"));
        Assert.Equal(1, viewer.Find("order", matchCase: true));

        viewer.OpenFind("Order");
        viewer.FindBarControl!.MatchCase.IsChecked = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, viewer.FindMatchCount);
    });

    [Fact(DisplayName = "Going to a match scrolls it into view, with room around it, and the bar stays at the top of the view")]
    public Task Match_ScrolledIntoView_BarStays() => session.RunAsync(() =>
    {
        var (_, scroller, viewer) = ShowScrolling(s_filler + "The needle.\n\n" + s_filler);

        viewer.OpenFind("needle");
        Dispatcher.UIThread.RunJobs();

        var text = Descendants<MarkdownTextBlock>(viewer).Single(block => block.AccessibleText == "The needle.");
        var y = text.TranslatePoint(default, scroller)!.Value.Y;
        Assert.True(scroller.Offset.Y > 0);
        Assert.InRange(y, 100, scroller.Viewport.Height - 100);
        Assert.InRange(viewer.FindBarControl!.TranslatePoint(default, scroller)!.Value.Y, 7, 9);
    });

    [Fact(DisplayName = "A match in a closed section opens it when it becomes the current match")]
    public Task Match_InClosedSection_Opens() => session.RunAsync(() =>
    {
        var (_, _, viewer) = ShowScrolling("Visible text.\n\n<details>\n<summary>More</summary>\n\nThe hidden needle.\n\n</details>");
        var expander = Descendants<Expander>(viewer).Single();
        Assert.False(expander.IsExpanded);

        Assert.Equal(1, viewer.Find("needle"));
        Dispatcher.UIThread.RunJobs();

        Assert.True(expander.IsExpanded);
    });

    [Fact(DisplayName = "As a streamed reply grows, the matches follow it")]
    public Task Matches_FollowText() => session.RunAsync(() =>
    {
        var (_, _, viewer) = ShowScrolling("one");
        viewer.Find("one");

        viewer.Markdown = "one\n\nand one more";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, viewer.FindMatchCount);
        Assert.Equal(1, viewer.CurrentFindMatch);
    });

    [Fact(DisplayName = "The bar's labels can be translated, and name its parts for assistive technologies")]
    public Task Labels() => session.RunAsync(() =>
    {
        var (_, _, viewer) = ShowScrolling("text");
        viewer.FindBarLabels = item => item == MarkdownFindBarItem.Field ? "尋找" : null;

        viewer.OpenFind();

        var bar = viewer.FindBarControl!;
        Assert.Equal("尋找", bar.Field.PlaceholderText);
        Assert.Equal("尋找", AutomationProperties.GetName(bar.Field));
        Assert.Equal("Match case", AutomationProperties.GetName(bar.MatchCase));
    });

    [Fact(DisplayName = "The reader's search button, at the top of its rail, opens the viewer's find bar, with or without headings")]
    public Task Reader_SearchButton() => session.RunAsync(() =>
    {
        var viewer = new MarkdownViewer { ImageLoader = NoImages, Markdown = "Just text." };
        var reader = new MarkdownReader { Viewer = viewer };
        var window = new Window { Width = 800, Height = 600, Content = reader };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var button = reader.GetVisualChildren().OfType<Button>().Single();
        Assert.Equal("Find", AutomationProperties.GetName(button));
        var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.True(viewer.IsFindOpen);
    });
}
