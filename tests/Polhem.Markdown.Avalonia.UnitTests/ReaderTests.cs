using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>
/// The reader around a viewer, with its table of contents: a rail along its edge, a list that slides out over the text,
/// and a pin that keeps it beside the text (polhem-dev/polhem-markdown#79).
/// </summary>
[Collection(HeadlessCollection.Name)]
public class ReaderTests(HeadlessSession session)
{
    private static readonly string s_filler = string.Concat(Enumerable.Repeat("A paragraph that takes up some room.\n\n", 40));
    private static readonly string s_document = "# Guide\n\n" + s_filler + "## Install\n\n" + s_filler + "### Options\n\n" + s_filler;

    private static (Window Window, MarkdownReader Reader, MarkdownViewer Viewer) ShowReader(
        string markdown, bool pinned = false, OutlineSide side = OutlineSide.Left)
    {
        var viewer = new MarkdownViewer { ImageLoader = NoImages, Markdown = markdown };
        var reader = new MarkdownReader { Viewer = viewer, OutlinePinned = pinned, OutlineSide = side };
        var window = new Window { Width = 1200, Height = 600, Content = reader };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, reader, viewer);
    }

    private static MarkdownTextBlock HeadingText(MarkdownViewer viewer, string id) =>
        Descendants<MarkdownTextBlock>(viewer).Single(text => text.GetValue(MarkdownViewer.AnchorIdsProperty).Contains(id));

    private static void Click(Window window, Control control)
    {
        var point = control.TranslatePoint(new Point(control.Bounds.Width / 2, Math.Min(control.Bounds.Height / 2, 20)), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact(DisplayName = "The reader scrolls the viewer in a column of the widest it allows, centred beside the rail")]
    public Task Reader_ScrollsCentredColumn() => session.RunAsync(() =>
    {
        var (_, reader, viewer) = ShowReader(s_document);

        Assert.Equal(new Rect(0, 0, OutlineRail.Size, 600), reader.Rail.Bounds);
        Assert.True(reader.Rail.IsVisible);
        Assert.False(reader.Outline.IsVisible);
        Assert.Equal(OutlineRail.Size, reader.Scroller.Bounds.X);
        var column = viewer.TranslatePoint(default, reader)!.Value;
        var right = reader.Bounds.Width - (column.X + viewer.Bounds.Width);
        Assert.True(viewer.Bounds.Width <= reader.TextMaxWidth);
        Assert.InRange(column.X - OutlineRail.Size - right, -20, 20);
        Assert.True(reader.Scroller.Extent.Height > reader.Scroller.Viewport.Height);
    });

    [Fact(DisplayName = "A text without headings has no rail, and the text takes the room")]
    public Task NoHeadings_NoRail() => session.RunAsync(() =>
    {
        var (_, reader, _) = ShowReader("Just text.");

        Assert.False(reader.Rail.IsVisible);
        Assert.Equal(0, reader.Scroller.Bounds.X);
    });

    [Fact(DisplayName = "Pointing at the rail slides the list out over the text, flush with the rail and as tall as the reader")]
    public Task Pointer_OpensList() => session.RunAsync(() =>
    {
        var (window, reader, _) = ShowReader(s_document);

        window.MouseMove(new Point(20, 100));
        Dispatcher.UIThread.RunJobs();

        Assert.True(reader.OutlineOpen);
        Assert.Equal(new Rect(OutlineRail.Size, 0, OutlinePanel.PanelWidth, 600), reader.Outline.Bounds);
        Assert.Equal(OutlineRail.Size, reader.Scroller.Bounds.X);
        Assert.Equal(["Guide", "Install", "Options"], reader.Outline.Entries.Select(entry => entry.Heading.Text));
    });

    [Fact(DisplayName = "A click on the rail opens the list to stay, with the focus in it, and Escape closes it and returns to the rail")]
    public Task Click_OpensList_EscapeCloses() => session.RunAsync(() =>
    {
        var (window, reader, _) = ShowReader(s_document);

        Click(window, reader.Rail);
        Dispatcher.UIThread.RunJobs();

        Assert.True(reader.OutlineOpen);
        Assert.Contains(reader.Outline.Entries, entry => entry.IsFocused);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.False(reader.OutlineOpen);
        Assert.True(reader.Rail.IsFocused);
    });

    [Fact(DisplayName = "Tab reaches the rail before the text's links, and Enter on it opens the list")]
    public Task Keyboard_RailFirst() => session.RunAsync(() =>
    {
        var (window, reader, _) = ShowReader("# Guide\n\nSee [the docs](https://example.org).");

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.True(reader.Rail.IsFocused);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.True(reader.OutlineOpen);
    });

    [Fact(DisplayName = "Picking a heading scrolls it to the top, closes the list, moves the focus there and marks it")]
    public Task Pick_ScrollsAndMarks() => session.RunAsync(() =>
    {
        var (window, reader, viewer) = ShowReader(s_document);
        Click(window, reader.Rail);

        Click(window, reader.Outline.Entries.Single(entry => entry.Heading.Id == "install"));

        var heading = HeadingText(viewer, "install");
        Assert.InRange(heading.TranslatePoint(default, reader.Scroller)!.Value.Y, -1, 1);
        Assert.False(reader.OutlineOpen);
        Assert.True(heading.IsFocused);
        Assert.Equal("install", reader.CurrentHeading?.Id);
        Assert.True(reader.Outline.Entries.Single(entry => entry.Heading.Id == "install").IsCurrent);
    });

    [Fact(DisplayName = "A click beside the list closes it")]
    public Task ClickBeside_Closes() => session.RunAsync(() =>
    {
        var (window, reader, _) = ShowReader(s_document);
        Click(window, reader.Rail);

        window.MouseDown(new Point(900, 300), MouseButton.Left);
        window.MouseUp(new Point(900, 300), MouseButton.Left);
        Dispatcher.UIThread.RunJobs();

        Assert.False(reader.OutlineOpen);
    });

    [Fact(DisplayName = "Pinned, the list is a column between the rail and the text, and the text centres in the room left")]
    public Task Pinned_Column() => session.RunAsync(() =>
    {
        var (_, reader, viewer) = ShowReader(s_document, pinned: true);

        Assert.True(reader.Outline.IsVisible);
        Assert.Equal(new Rect(OutlineRail.Size, 0, OutlinePanel.PanelWidth, 600), reader.Outline.Bounds);
        Assert.Equal(OutlineRail.Size + OutlinePanel.PanelWidth, reader.Scroller.Bounds.X);
        var column = viewer.TranslatePoint(default, reader)!.Value;
        var right = reader.Bounds.Width - (column.X + viewer.Bounds.Width);
        Assert.InRange(column.X - reader.Scroller.Bounds.X - right, -20, 20);
    });

    [Fact(DisplayName = "As the text scrolls, the section being read is marked, the bottom of the view deciding at the end")]
    public Task Scroll_MarksSection() => session.RunAsync(() =>
    {
        var (_, reader, viewer) = ShowReader(s_document, pinned: true);

        Assert.True(viewer.ScrollToHeading("install"));
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("install", reader.CurrentHeading?.Id);

        reader.Scroller.Offset = new Vector(0, reader.Scroller.Extent.Height);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("options", reader.CurrentHeading?.Id);
    });

    [Fact(DisplayName = "The pin pins and unpins the list, and the change reaches a binding")]
    public Task Pin_TogglesPinned() => session.RunAsync(() =>
    {
        var (window, reader, _) = ShowReader(s_document);
        var changes = new List<bool>();
        reader.PropertyChanged += (_, e) =>
        {
            if (e.Property == MarkdownReader.OutlinePinnedProperty)
            {
                changes.Add(reader.OutlinePinned);
            }
        };
        Click(window, reader.Rail);
        Assert.Equal(reader.PinOutlineLabel, AutomationProperties.GetName(reader.Outline.PinButton));

        Click(window, reader.Outline.PinButton);
        Assert.True(reader.OutlinePinned);
        Assert.Equal(reader.UnpinOutlineLabel, AutomationProperties.GetName(reader.Outline.PinButton));

        Click(window, reader.Outline.PinButton);
        Assert.False(reader.OutlinePinned);
        Assert.Equal([true, false], changes);
    });

    [Fact(DisplayName = "The pin leans as an outline while the list floats, and stands upright and solid while it is pinned")]
    public Task Pin_IconShowsState() => session.RunAsync(() =>
    {
        var (_, reader, _) = ShowReader(s_document);
        var icon = reader.Outline.PinIcon;

        Assert.Null(icon.Fill);
        Assert.NotNull(icon.Stroke);
        Assert.Equal(45, Assert.IsType<RotateTransform>(icon.RenderTransform).Angle);

        reader.OutlinePinned = true;
        Assert.Same(icon.Stroke, icon.Fill);
        Assert.Null(icon.RenderTransform);

        reader.OutlinePinned = false;
        Assert.Null(icon.Fill);
        Assert.IsType<RotateTransform>(icon.RenderTransform);
    });

    [Fact(DisplayName = "On the right, the rail and the column are on the right")]
    public Task RightSide() => session.RunAsync(() =>
    {
        var (_, reader, _) = ShowReader(s_document, pinned: true, side: OutlineSide.Right);

        Assert.Equal(reader.Bounds.Width, reader.Rail.Bounds.Right);
        Assert.Equal(reader.Rail.Bounds.X, reader.Outline.Bounds.Right);
        Assert.Equal(0, reader.Scroller.Bounds.X);
        Assert.Equal(reader.Outline.Bounds.X, reader.Scroller.Bounds.Right);
    });

    [Fact(DisplayName = "The list follows the text as it changes, as a reply that streams in")]
    public Task Headings_FollowText() => session.RunAsync(() =>
    {
        var (_, reader, viewer) = ShowReader("# One\n\ntext", pinned: true);

        viewer.Markdown = "# One\n\ntext\n\n## Two";
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(["One", "Two"], reader.Outline.Entries.Select(entry => entry.Heading.Text));
    });

    [Fact(DisplayName = "The rail is a button named by the label that opens the list when pressed, the list a navigation landmark, and each entry a button named by its heading")]
    public Task Automation() => session.RunAsync(() =>
    {
        var (_, reader, _) = ShowReader(s_document, pinned: true);
        reader.OutlineLabel = "目錄";

        var rail = ControlAutomationPeer.CreatePeerForElement(reader.Rail);
        Assert.Equal(AutomationControlType.Button, rail.GetAutomationControlType());
        Assert.Equal("目錄", rail.GetName());
        reader.OutlinePinned = false;
        rail.GetProvider<global::Avalonia.Automation.Provider.IInvokeProvider>()!.Invoke();
        Assert.True(reader.OutlineOpen);
        reader.OutlinePinned = true;
        var panel = ControlAutomationPeer.CreatePeerForElement(reader.Outline);
        Assert.Equal(AutomationLandmarkType.Navigation, panel.GetLandmarkType());
        Assert.Equal("目錄", panel.GetName());
        var entry = ControlAutomationPeer.CreatePeerForElement(reader.Outline.Entries.First());
        Assert.Equal("Guide", entry.GetName());
    });
}
