using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;
using Path = Avalonia.Controls.Shapes.Path;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class CodeBlockActionTests(HeadlessSession session)
{
    private const string TwoBlocks = "```markdown\n# title\n```\n\n```csharp\nvar x = 1;\n```";

    private static List<CodeBlockView> CodeBlocks(MarkdownViewer viewer) => Blocks(viewer).OfType<CodeBlockView>().ToList();

    private static List<Button> ActionButtons(CodeBlockView code) => code.ActionsPanel.Children.OfType<Button>().ToList();

    private static void Click(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Dispatcher.UIThread.RunJobs();
    }

    [Fact(DisplayName = "Without actions a code block shows only its copy button")]
    public Task NoActions_ShowsOnlyCopy() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```\nx\n```");

        var code = Assert.Single(CodeBlocks(viewer));
        Assert.Empty(code.ActionsPanel.Children);
        Assert.Equal(2, Grid.GetColumn(code.CopyButton));
    });

    [Fact(DisplayName = "An action's button sits before the copy button and passes the block's code and language")]
    public Task Action_Click_ReceivesCodeAndLanguage() => session.RunAsync(() =>
    {
        MarkdownCodeBlock? received = null;
        var (_, viewer) = Show("```csharp\nvar x = 1;\n```");
        viewer.CodeBlockActions = [new MarkdownCodeBlockAction("Insert", block => received = block)];

        var code = Assert.Single(CodeBlocks(viewer));
        var button = Assert.Single(ActionButtons(code));
        Assert.True(Grid.GetColumn(code.ActionsPanel) < Grid.GetColumn(code.CopyButton));
        Click(button);

        Assert.NotNull(received);
        Assert.Equal("var x = 1;", received.Code);
        Assert.Equal("csharp", received.Language);
    });

    [Fact(DisplayName = "An action whose work is awaited runs when its button is clicked")]
    public Task AsyncAction_Click_Runs() => session.RunAsync(() =>
    {
        var completed = new TaskCompletionSource<string>();
        var (_, viewer) = Show("```\nx\n```");
        viewer.CodeBlockActions = [new MarkdownCodeBlockAction("Run", async block =>
        {
            await Task.Yield();
            completed.SetResult(block.Code);
        })];

        Click(Assert.Single(ActionButtons(Assert.Single(CodeBlocks(viewer)))));

        Assert.Equal("x", completed.Task.Result);
    });

    [Fact(DisplayName = "An action shows only on the blocks its visibility test accepts")]
    public Task Action_IsVisible_ChoosesBlocks() => session.RunAsync(() =>
    {
        var (_, viewer) = Show(TwoBlocks);
        viewer.CodeBlockActions =
        [
            new MarkdownCodeBlockAction("Insert", _ => { }) { IsVisible = block => block.Language == "markdown" },
        ];

        Assert.Equal([1, 0], CodeBlocks(viewer).Select(code => code.ActionsPanel.Children.Count));
    });

    [Fact(DisplayName = "Refreshing the actions asks their visibility again without building the blocks again")]
    public Task Refresh_AsksVisibilityAgain() => session.RunAsync(() =>
    {
        var editorHasFocus = false;
        var (_, viewer) = Show(TwoBlocks);
        viewer.CodeBlockActions = [new MarkdownCodeBlockAction("Insert", _ => { }) { IsVisible = _ => editorHasFocus }];
        var blocks = CodeBlocks(viewer);
        Assert.All(blocks, code => Assert.Empty(code.ActionsPanel.Children));

        editorHasFocus = true;
        viewer.RefreshCodeBlockActions();

        Assert.Equal(blocks, CodeBlocks(viewer));
        Assert.All(blocks, code => Assert.Single(code.ActionsPanel.Children));
    });

    [Fact(DisplayName = "A block built after the actions are set shows them")]
    public Task NewBlock_ShowsActions() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("text");
        viewer.CodeBlockActions = [new MarkdownCodeBlockAction("Insert", _ => { })];

        viewer.Markdown = "text\n\n```\nx\n```";
        Dispatcher.UIThread.RunJobs();

        Assert.Single(ActionButtons(Assert.Single(CodeBlocks(viewer))));
    });

    [Fact(DisplayName = "An action without an icon shows its label, which is also the name a screen reader reads")]
    public Task TextAction_ShowsLabel() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```\nx\n```");
        viewer.CodeBlockActions = [new MarkdownCodeBlockAction("Insert at cursor", _ => { })];

        var button = Assert.Single(ActionButtons(Assert.Single(CodeBlocks(viewer))));
        Assert.Equal("Insert at cursor", Assert.IsType<TextBlock>(button.Content).Text);
        Assert.Equal("Insert at cursor", AutomationProperties.GetName(button));
        Assert.True(button.IsTabStop);
        Assert.True(button.Focusable);
    });

    [Fact(DisplayName = "An action with an icon shows the icon, with its label as the tooltip and the accessible name")]
    public Task IconAction_ShowsIconWithLabel() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```\nx\n```");
        viewer.CodeBlockActions = [new MarkdownCodeBlockAction("Run", _ => { }) { IconPath = "M8 5v14l11-7z" }];

        var button = Assert.Single(ActionButtons(Assert.Single(CodeBlocks(viewer))));
        Assert.IsType<Path>(button.Content);
        Assert.Equal("Run", ToolTip.GetTip(button));
        Assert.Equal("Run", AutomationProperties.GetName(button));
    });

    [Fact(DisplayName = "An icon that is not path data shows the label instead of failing")]
    public Task BadIcon_ShowsLabel() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```\nx\n```");
        viewer.CodeBlockActions = [new MarkdownCodeBlockAction("Run", _ => { }) { IconPath = "not a path" }];

        var button = Assert.Single(ActionButtons(Assert.Single(CodeBlocks(viewer))));
        Assert.Equal("Run", Assert.IsType<TextBlock>(button.Content).Text);
    });

    [Fact(DisplayName = "Removing the actions removes their buttons")]
    public Task ClearActions_RemovesButtons() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```\nx\n```");
        viewer.CodeBlockActions = [new MarkdownCodeBlockAction("Insert", _ => { })];

        viewer.CodeBlockActions = null;

        Assert.Empty(Assert.Single(CodeBlocks(viewer)).ActionsPanel.Children);
    });

    [Fact(DisplayName = "Tab reaches an action's button before the copy button, and Enter runs the action")]
    public Task Keyboard_ReachesAndRunsAction() => session.RunAsync(() =>
    {
        var runs = 0;
        var (window, viewer) = Show("```\nx\n```");
        viewer.CodeBlockActions = [new MarkdownCodeBlockAction("Insert", _ => runs++)];
        Dispatcher.UIThread.RunJobs();

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.Equal("Insert", FocusedName(window));
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, runs);

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Assert.Equal("Copy", FocusedName(window));
    });

    private static string? FocusedName(Window window) =>
        window.FocusManager!.GetFocusedElement() is Control control ? AutomationProperties.GetName(control) : null;
}
