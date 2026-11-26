using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Polhem.Markdown.Avalonia.Formulas;
using Polhem.Markdown.Tests.Shared;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>
/// What the viewer tells an assistive technology, and how the keyboard moves through it
/// (polhem-dev/polhem-markdown#70).
/// </summary>
[Collection(HeadlessCollection.Name)]
public class AccessibilityTests(HeadlessSession session)
{
    [Fact(DisplayName = "Every node type of the model has a role, the same one the Blazor viewer shows it with")]
    public void Roles_CoverEveryNodeType()
    {
        Assert.Equal(ModelNodeTypes.All.OrderBy(type => type.Name), AccessibleRoles.ByNodeType.Keys.OrderBy(type => type.Name));
    }

    [Fact(DisplayName = "The viewer shows each node type with its role")]
    public Task Roles_EachNodeTypeShownWithItsRole() => session.RunAsync(() =>
    {
        var missing = new List<string>();
        foreach (var (type, (markdown, role)) in AccessibleRoles.ByNodeType)
        {
            if (role == AccessibleRole.None)
            {
                continue;
            }
            var (window, viewer) = Show(markdown);
            if (!Peers(viewer).Any(peer => RoleOf(peer, viewer) == role))
            {
                missing.Add(type.Name + " as " + role);
            }
            window.Close();
        }
        Assert.Empty(missing);
    });

    [Fact(DisplayName = "A heading is shown with its level")]
    public Task Heading_HasLevel() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("# One\n\n### Three\n\ntext");

        Assert.Equal([1, 3], Peers(viewer).Where(peer => peer.GetHeadingLevel() > 0).Select(peer => peer.GetHeadingLevel()));
        Assert.Equal(["One", "Three"], Peers(viewer).Where(peer => peer.GetHeadingLevel() > 0).Select(peer => peer.GetName()));
    });

    [Fact(DisplayName = "A text is named by all of it, with an image as its alternative text and a formula as its LaTeX")]
    public Task Text_NamedWithImagesAndFormulas() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("A ![cat](https://example.org/cat.png) and $x^2$.");

        Assert.Contains(Peers(viewer), peer => peer.GetName() == "A cat and $x^2$.");
        Assert.Contains(Peers(viewer), peer => peer.GetAutomationControlType() == AutomationControlType.Image && peer.GetName() == "cat");
    });

    [Fact(DisplayName = "An image with no alternative text is decorative and left out")]
    public Task Image_WithoutAlternativeText_LeftOut() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("![](https://example.org/line.png)");

        Assert.DoesNotContain(Peers(viewer), peer => peer.GetAutomationControlType() == AutomationControlType.Image);
    });

    [Fact(DisplayName = "Each link of a text is a hyperlink named by its text, and invoking it follows it")]
    public Task Link_IsHyperlinkThatCanBeInvoked() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("See [the docs](https://example.org/docs) or [the FAQ](https://example.org/faq).");
        var opened = new List<Uri>();
        viewer.LinkActivated += (_, e) =>
        {
            opened.Add(e.Uri);
            e.Handled = true;
        };

        var links = Peers(viewer).Where(peer => peer.GetAutomationControlType() == AutomationControlType.Hyperlink).ToList();
        Assert.Equal(["the docs", "the FAQ"], links.Select(link => link.GetName()));
        Assert.All(links, link => Assert.True(link.IsKeyboardFocusable()));

        links[1].GetProvider<IInvokeProvider>()!.Invoke();
        Assert.Equal([new Uri("https://example.org/faq")], opened);
    });

    [Fact(DisplayName = "A footnote's back link is named as in the Blazor viewer, not by its arrow")]
    public Task FootnoteBackLink_Named() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("Text[^1] and more[^1].\n\n[^1]: A note.");

        Assert.Equal(
            ["1", "1", "Back to reference", "Back to reference 2"],
            Peers(viewer).Where(peer => peer.GetAutomationControlType() == AutomationControlType.Hyperlink).Select(peer => peer.GetName()));
    });

    [Fact(DisplayName = "A list's items are list items that know their place, with the task state and the number but no bullet")]
    public Task List_ItemsWithPositionAndTaskState() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("- [x] done\n- [ ] open\n\n3. three\n4. four");

        var lists = Peers(viewer).Where(peer => peer.GetAutomationControlType() == AutomationControlType.List).ToList();
        Assert.Equal(2, lists.Count);
        var items = lists[0].GetChildren();
        Assert.All(items, item => Assert.Equal(AutomationControlType.ListItem, item.GetAutomationControlType()));
        Assert.Equal([1, 2], items.Select(item => AutomationProperties.GetPositionInSet(((ControlAutomationPeer)item).Owner)));
        Assert.Equal([2, 2], items.Select(item => AutomationProperties.GetSizeOfSet(((ControlAutomationPeer)item).Owner)));

        var boxes = Peers(lists[0]).Select(peer => peer.GetProvider<IToggleProvider>()).OfType<IToggleProvider>().ToList();
        Assert.Equal([ToggleState.On, ToggleState.Off], boxes.Select(box => box.ToggleState));

        Assert.DoesNotContain(Peers(viewer), peer => peer.GetName() == "•");
        Assert.Contains(Peers(lists[1]), peer => peer.GetName() == "3.");
    });

    [Fact(DisplayName = "A table is a table of header cells and cells, the header cells being plain cells on macOS")]
    public Task Table_HasHeaderCellsAndCells() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("| Name | Size |\n|---|---|\n| a | 1 |");

        var table = Assert.Single(Peers(viewer), peer => peer.GetAutomationControlType() == AutomationControlType.Table);
        var header = OperatingSystem.IsMacOS() ? AutomationControlType.DataItem : AutomationControlType.HeaderItem;
        Assert.Equal(
            [header, header, AutomationControlType.DataItem, AutomationControlType.DataItem],
            table.GetChildren().Select(cell => cell.GetAutomationControlType()));
        Assert.Equal(["Name", "Size", "a", "1"], table.GetChildren().Select(cell => Assert.Single(cell.GetChildren()).GetName()));
    });

    [Fact(DisplayName = "An alert is a group named by its kind, and a collapsible section's header by its summary")]
    public Task GroupsNamed() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("> [!WARNING]\n> Careful.\n\n<details>\n<summary>More detail</summary>\n\nHidden.\n\n</details>");

        Assert.Contains(Peers(viewer), peer => peer.GetAutomationControlType() == AutomationControlType.Group && peer.GetName() == "Warning");
        Assert.DoesNotContain(Peers(viewer), peer => peer.GetAutomationControlType() == AutomationControlType.Text && peer.GetName() == "Warning");
        Assert.Contains(Peers(viewer), peer => peer.GetAutomationControlType() == AutomationControlType.Button && peer.GetName() == "More detail");
    });

    [Fact(DisplayName = "Tab moves through links and buttons in the order of the text, and passes over text without links")]
    public Task Tab_MovesThroughLinksAndButtons() => session.RunAsync(() =>
    {
        var (window, viewer) = Show(
            "Plain text.\n\n[one](https://example.org/1) and [two](https://example.org/2)\n\n```\ncode\n```\n\nMore plain text.\n\n[three](https://example.org/3)");

        var stops = new List<string>();
        for (var press = 0; press < 5; press++)
        {
            window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
            stops.Add(FocusedStop(window));
        }
        Assert.Equal(["one", "two", "Copy", "three", "one"], stops);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.Equal("three", FocusedStop(window));
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.Equal("Copy", FocusedStop(window));
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.Shift);
        Assert.Equal("two", FocusedStop(window));
    });

    [Fact(DisplayName = "The link the keyboard is on has the keyboard focus for an assistive technology")]
    public Task FocusedLink_HasKeyboardFocus() => session.RunAsync(() =>
    {
        var (window, viewer) = Show("[one](https://example.org/1) and [two](https://example.org/2)");

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);

        var links = Peers(viewer).Where(peer => peer.GetAutomationControlType() == AutomationControlType.Hyperlink).ToList();
        Assert.Equal([false, true], links.Select(link => link.HasKeyboardFocus()));
    });

    [Fact(DisplayName = "A link keeps the focus when its text is laid out again, as when the window is resized")]
    public Task FocusedLink_KeepsFocusOnRelayout() => session.RunAsync(() =>
    {
        var (window, _) = Show("A ![cat](https://example.org/cat.png) and [one](https://example.org/1) and $x$ in a line.");

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.Equal("one", FocusedStop(window));

        window.Width = 300;
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("one", FocusedStop(window));
    });

    [Fact(DisplayName = "Enter follows the link the keyboard is on")]
    public Task Enter_FollowsLink() => session.RunAsync(() =>
    {
        var (window, viewer) = Show("[one](https://example.org/1) and [two](https://example.org/2)");
        var opened = new List<Uri>();
        viewer.LinkActivated += (_, e) =>
        {
            opened.Add(e.Uri);
            e.Handled = true;
        };

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);

        Assert.Equal([new Uri("https://example.org/2")], opened);
    });

    [Fact(DisplayName = "Following a footnote reference from the keyboard moves the focus to the note, and Tab goes on from there")]
    public Task Enter_OnFootnoteReference_MovesFocusToNote() => session.RunAsync(() =>
    {
        var (window, _) = Show("Text[^1].\n\n[later](https://example.org/later)\n\n[^1]: A note.");

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        var focused = Assert.IsType<MarkdownTextBlock>(window.FocusManager!.GetFocusedElement());
        Assert.StartsWith("A note.", focused.AccessibleText, StringComparison.Ordinal);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.Equal("Back to reference", FocusedStop(window));
    });

    [Fact(DisplayName = "A click on a text gives it the focus but no link, as a click on text in a browser does")]
    public Task Click_FocusesTextWithoutLink() => session.RunAsync(() =>
    {
        var (window, viewer) = Show("Some text before [a link](https://example.org).");
        var text = Descendants<MarkdownTextBlock>(viewer).Single();

        var point = text.TranslatePoint(new Point(4, text.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);

        Assert.True(text.IsFocused);
        Assert.Equal(-1, text.FocusedLink);
    });

    /// <summary>The name of the focused link, or of the focused control.</summary>
    private static string FocusedStop(Window window) => window.FocusManager!.GetFocusedElement() switch
    {
        LinkFocusTarget link => link.LinkName,
        Control control => ControlAutomationPeer.CreatePeerForElement(control).GetName() ?? control.GetType().Name,
        var other => other?.GetType().Name ?? "nothing",
    };

    /// <summary>
    /// The peers below a control's that an assistive technology shows, depth first, in the order it reads them. A peer
    /// that is no control element, such as a layout panel's, is passed over but its children are not.
    /// </summary>
    private static IEnumerable<AutomationPeer> Peers(Control control) => Peers(ControlAutomationPeer.CreatePeerForElement(control));

    private static IEnumerable<AutomationPeer> Peers(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren())
        {
            if (child.IsControlElement())
            {
                yield return child;
            }
            foreach (var descendant in Peers(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsInHeaderRow(AutomationPeer cell) =>
        ((ControlAutomationPeer)cell).Owner is Border { Child: { } text } && text.GetValue(MarkdownViewer.TableRowProperty) == 0;

    private static AccessibleRole RoleOf(AutomationPeer peer, MarkdownViewer viewer)
    {
        var owner = (peer as ControlAutomationPeer)?.Owner;
        return peer.GetAutomationControlType() switch
        {
            _ when peer.GetHeadingLevel() > 0 => AccessibleRole.Heading,
            _ when owner is FormulaView && !string.IsNullOrEmpty(peer.GetName()) => AccessibleRole.Formula,
            AutomationControlType.Text when owner is MarkdownTextBlock => AccessibleRole.Text,
            AutomationControlType.Hyperlink => AccessibleRole.Link,
            AutomationControlType.List => AccessibleRole.List,
            AutomationControlType.ListItem => AccessibleRole.ListItem,
            AutomationControlType.Table => AccessibleRole.Table,
            AutomationControlType.HeaderItem => AccessibleRole.HeaderCell,

            // On macOS a header cell is a cell too; a table's header row is its first.
            AutomationControlType.DataItem when OperatingSystem.IsMacOS() && IsInHeaderRow(peer) => AccessibleRole.HeaderCell,
            AutomationControlType.DataItem => AccessibleRole.Cell,
            AutomationControlType.Image when !string.IsNullOrEmpty(peer.GetName()) => AccessibleRole.Image,
            AutomationControlType.Group => AccessibleRole.Group,
            AutomationControlType.Expander => AccessibleRole.Disclosure,
            AutomationControlType.Separator => AccessibleRole.Separator,
            AutomationControlType.Button when peer.GetName() == viewer.CopyCodeLabel => AccessibleRole.Code,
            _ => AccessibleRole.None,
        };
    }
}
