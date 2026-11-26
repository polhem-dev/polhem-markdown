using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>
/// The table of contents the viewer offers: floating from a button, or pinned beside the text
/// (polhem-dev/polhem-markdown#79).
/// </summary>
[Collection(HeadlessCollection.Name)]
public class OutlineTests(HeadlessSession session)
{
    private static readonly string s_filler = string.Concat(Enumerable.Repeat("A paragraph that takes up some room.\n\n", 40));
    private static readonly string s_document = "# Guide\n\n" + s_filler + "## Install\n\n" + s_filler + "### Options\n\n" + s_filler;

    /// <summary>Shows a viewer with the table of contents on, in a scroll viewer, as a host would.</summary>
    private static (Window Window, ScrollViewer Scroller, MarkdownViewer Viewer) ShowScrolling(
        string markdown, bool pinned = false, OutlineSide side = OutlineSide.Left)
    {
        var viewer = new MarkdownViewer
        {
            ImageLoader = NoImages,
            Markdown = markdown,
            ShowOutline = true,
            OutlinePinned = pinned,
            OutlineSide = side,
        };
        var scroller = new ScrollViewer { Content = viewer };
        var window = new Window { Width = 800, Height = 600, Content = scroller };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, scroller, viewer);
    }

    private static MarkdownTextBlock HeadingText(MarkdownViewer viewer, string id) =>
        Descendants<MarkdownTextBlock>(viewer).Single(text => text.GetValue(MarkdownViewer.AnchorIdsProperty).Contains(id));

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact(DisplayName = "Off by default: no button, and the text takes the whole width")]
    public Task Off_ByDefault() => session.RunAsync(() =>
    {
        var (_, viewer) = Show(s_document);

        Assert.Null(viewer.OutlineButton);
        Assert.Equal(viewer.Bounds.Width, Blocks(viewer).First().Bounds.Width);
    });

    [Fact(DisplayName = "On, a button in the top corner leaves room for itself beside the text; a text without headings has none")]
    public Task On_ButtonBesideText() => session.RunAsync(() =>
    {
        var (_, _, viewer) = ShowScrolling(s_document);

        var button = viewer.OutlineButton!;
        Assert.True(button.IsVisible);
        Assert.Equal(new Point(0, 0), button.Bounds.Position);
        Assert.Equal(AutomationProperties.GetName(button), viewer.OutlineLabel);
        var text = (Control)viewer.GetVisualChildren().OfType<StackPanel>().First();
        Assert.True(text.Bounds.X >= button.Bounds.Right);

        viewer.Markdown = "Just text.";
        Dispatcher.UIThread.RunJobs();
        Assert.False(button.IsVisible);
        Assert.Equal(0, text.Bounds.X);
    });

    [Fact(DisplayName = "The button opens the list floating over the text, indented by level, and Escape closes it and returns to the button")]
    public Task Button_OpensFloatingList() => session.RunAsync(() =>
    {
        var (window, _, viewer) = ShowScrolling(s_document);

        Click(window, viewer.OutlineButton!);

        var panel = viewer.Outline!;
        Assert.True(viewer.OutlineOpen);
        Assert.True(panel.IsVisible);
        Assert.Equal(["Guide", "Install", "Options"], panel.Entries.Select(entry => entry.Heading.Text));
        var indents = panel.Entries.Select(entry => entry.TranslatePoint(default, panel)!.Value.X + ((Border)entry.GetVisualChildren().Single()).Padding.Left).ToList();
        Assert.True(indents[0] < indents[1] && indents[1] < indents[2]);
        Assert.Contains(panel.Entries, entry => entry.IsFocused);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(viewer.OutlineOpen);
        Assert.False(panel.IsVisible);
        Assert.True(viewer.OutlineButton!.IsFocused);
    });

    [Fact(DisplayName = "Picking a heading scrolls it to the top, closes the floating list, moves the focus there and marks it")]
    public Task Pick_ScrollsAndMarks() => session.RunAsync(() =>
    {
        var (window, scroller, viewer) = ShowScrolling(s_document);
        Click(window, viewer.OutlineButton!);

        Click(window, viewer.Outline!.Entries.Single(entry => entry.Heading.Id == "install"));

        var heading = HeadingText(viewer, "install");
        Assert.InRange(heading.TranslatePoint(default, scroller)!.Value.Y, -1, 1);
        Assert.False(viewer.OutlineOpen);
        Assert.True(heading.IsFocused);
        Assert.Equal("install", viewer.CurrentHeading?.Id);
        Assert.True(viewer.Outline.Entries.Single(entry => entry.Heading.Id == "install").IsCurrent);
    });

    [Fact(DisplayName = "Tab reaches the button before the text's links, as the Blazor viewer's navigation comes first")]
    public Task Tab_ReachesButtonFirst() => session.RunAsync(() =>
    {
        var (window, _, viewer) = ShowScrolling("# Guide\n\nSee [the docs](https://example.org).");

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

        Assert.True(viewer.OutlineButton!.IsFocused);
    });

    [Fact(DisplayName = "A click beside the floating list closes it")]
    public Task ClickBeside_Closes() => session.RunAsync(() =>
    {
        var (window, _, viewer) = ShowScrolling(s_document);
        Click(window, viewer.OutlineButton!);

        window.MouseDown(new Point(700, 400), MouseButton.Left);
        window.MouseUp(new Point(700, 400), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(viewer.OutlineOpen);
    });

    [Fact(DisplayName = "Pinned, the list is a column beside the text that stays in view as the text scrolls, and marks the section being read")]
    public Task Pinned_ColumnStaysInView() => session.RunAsync(() =>
    {
        var (_, scroller, viewer) = ShowScrolling(s_document, pinned: true);
        var panel = viewer.Outline!;
        var text = (Control)viewer.GetVisualChildren().OfType<StackPanel>().First();

        Assert.True(panel.IsVisible);
        Assert.False(viewer.OutlineButton!.IsVisible);
        Assert.True(text.Bounds.X >= panel.Bounds.Right);

        Assert.True(viewer.ScrollToHeading("install"));
        Dispatcher.UIThread.RunJobs();

        Assert.True(scroller.Offset.Y > 0);
        Assert.InRange(panel.TranslatePoint(default, scroller)!.Value.Y, -1, 1);
        Assert.Equal("install", viewer.CurrentHeading?.Id);

        scroller.Offset = new Vector(0, scroller.Extent.Height);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("options", viewer.CurrentHeading?.Id);
    });

    [Fact(DisplayName = "The pin button pins and unpins the list, and the change reaches a binding")]
    public Task PinButton_TogglesPinned() => session.RunAsync(() =>
    {
        var (window, _, viewer) = ShowScrolling(s_document);
        var changes = new List<bool>();
        viewer.PropertyChanged += (_, e) =>
        {
            if (e.Property == MarkdownViewer.OutlinePinnedProperty)
            {
                changes.Add(viewer.OutlinePinned);
            }
        };
        Click(window, viewer.OutlineButton!);
        var pin = viewer.Outline!.GetLogicalDescendants().OfType<ToggleButton>().Single();
        Assert.Equal(viewer.PinOutlineLabel, AutomationProperties.GetName(pin));

        Click(window, pin);
        Assert.True(viewer.OutlinePinned);
        Assert.Equal(viewer.UnpinOutlineLabel, AutomationProperties.GetName(pin));

        Click(window, pin);
        Assert.False(viewer.OutlinePinned);
        Assert.Equal([true, false], changes);
    });

    [Fact(DisplayName = "On the right, the button and the column are on the right of the text")]
    public Task RightSide() => session.RunAsync(() =>
    {
        var (_, _, viewer) = ShowScrolling(s_document, side: OutlineSide.Right);
        var text = (Control)viewer.GetVisualChildren().OfType<StackPanel>().First();

        Assert.Equal(viewer.Bounds.Width, viewer.OutlineButton!.Bounds.Right, 1);
        Assert.Equal(0, text.Bounds.X);

        viewer.OutlinePinned = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(viewer.Bounds.Width, viewer.Outline!.Bounds.Right, 1);
        Assert.True(text.Bounds.Right <= viewer.Outline.Bounds.X);
    });

    [Fact(DisplayName = "The list follows the text as it changes, as a reply that streams in")]
    public Task Headings_FollowText() => session.RunAsync(() =>
    {
        var (_, _, viewer) = ShowScrolling("# One\n\ntext", pinned: true);

        viewer.Markdown = "# One\n\ntext\n\n## Two";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["One", "Two"], viewer.Outline!.Entries.Select(entry => entry.Heading.Text));
    });

    [Fact(DisplayName = "The list is a navigation landmark named by its label, and each entry is a button named by its heading")]
    public Task Automation_LandmarkAndEntries() => session.RunAsync(() =>
    {
        var (_, _, viewer) = ShowScrolling(s_document, pinned: true);
        viewer.OutlineLabel = "目錄";

        var peer = ControlAutomationPeer.CreatePeerForElement(viewer.Outline!);
        Assert.Equal(AutomationLandmarkType.Navigation, peer.GetLandmarkType());
        Assert.Equal("目錄", peer.GetName());
        var entry = ControlAutomationPeer.CreatePeerForElement(viewer.Outline!.Entries.First());
        Assert.Equal(AutomationControlType.Button, entry.GetAutomationControlType());
        Assert.Equal("Guide", entry.GetName());
    });
}
