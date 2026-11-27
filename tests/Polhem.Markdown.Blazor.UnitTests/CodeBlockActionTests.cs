using Bunit;

namespace Polhem.Markdown.Blazor.UnitTests;

public class CodeBlockActionTests : BunitContext
{
    private const string TwoBlocks = "```markdown\n# title\n```\n\n```csharp\nvar x = 1;\n```";

    private IRenderedComponent<MarkdownViewer> Show(string markdown, IReadOnlyList<MarkdownCodeBlockAction>? actions) =>
        Render<MarkdownViewer>(parameters => parameters
            .Add(viewer => viewer.Markdown, markdown)
            .Add(viewer => viewer.CodeBlockActions, actions));

    [Fact(DisplayName = "Without actions a code block's header is the language and the copy button")]
    public void NoActions_HeaderUnchanged()
    {
        var viewer = Show("```csharp\nx\n```", null);

        Assert.Equal(["span", "button"], viewer.FindAll(".pmd-code-header > *").Select(element => element.LocalName));
        Assert.Empty(viewer.FindAll(".pmd-code-action"));
    }

    [Fact(DisplayName = "An action's button sits before the copy button and passes the block's code and language")]
    public void Action_Click_ReceivesCodeAndLanguage()
    {
        MarkdownCodeBlock? received = null;
        var viewer = Show("```csharp\nvar x = 1;\n```", [new MarkdownCodeBlockAction("Insert", block => received = block)]);

        Assert.Equal(["pmd-code-lang", "pmd-code-action", "pmd-copy"],
            viewer.FindAll(".pmd-code-header > *").Select(element => element.ClassName));
        viewer.Find("button.pmd-code-action").Click();

        Assert.NotNull(received);
        Assert.Equal("var x = 1;", received.Code);
        Assert.Equal("csharp", received.Language);
    }

    [Fact(DisplayName = "An action whose work is awaited runs when its button is clicked")]
    public async Task AsyncAction_Click_Runs()
    {
        var completed = new TaskCompletionSource<string>();
        var viewer = Show("```\nx\n```", [new MarkdownCodeBlockAction("Run", async block =>
        {
            await Task.Yield();
            completed.SetResult(block.Code);
        })]);

        await viewer.Find("button.pmd-code-action").ClickAsync(new());

        Assert.Equal("x", await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact(DisplayName = "An action shows only on the blocks its visibility test accepts")]
    public void Action_IsVisible_ChoosesBlocks()
    {
        var viewer = Show(TwoBlocks,
            [new MarkdownCodeBlockAction("Insert", _ => { }) { IsVisible = block => block.Language == "markdown" }]);

        Assert.Equal([1, 0], viewer.FindAll(".pmd-code").Select(code => code.QuerySelectorAll(".pmd-code-action").Length));
    }

    [Fact(DisplayName = "Refreshing the actions asks their visibility again and renders only the blocks with code")]
    public async Task Refresh_AsksVisibilityAgain()
    {
        var editorHasFocus = false;
        var viewer = Show("text\n\n" + TwoBlocks,
            [new MarkdownCodeBlockAction("Insert", _ => { }) { IsVisible = _ => editorHasFocus }]);
        Assert.Empty(viewer.FindAll(".pmd-code-action"));
        var before = viewer.Instance.BlockRenderCount;

        editorHasFocus = true;
        await viewer.Instance.RefreshCodeBlockActionsAsync();

        Assert.Equal(2, viewer.FindAll(".pmd-code-action").Count);
        Assert.Equal(2, viewer.Instance.BlockRenderCount - before);
    }

    [Fact(DisplayName = "The same list of actions on a render of the host renders no block again")]
    public void SameActions_RenderNothingAgain()
    {
        IReadOnlyList<MarkdownCodeBlockAction> actions = [new MarkdownCodeBlockAction("Insert", _ => { })];
        var viewer = Show(TwoBlocks, actions);
        var before = viewer.Instance.BlockRenderCount;

        viewer.Render(parameters => parameters.Add(component => component.CodeBlockActions, actions));

        Assert.Equal(0, viewer.Instance.BlockRenderCount - before);
    }

    [Fact(DisplayName = "An action without an icon shows its label as the button's text, which a screen reader reads")]
    public void TextAction_ShowsLabel()
    {
        var viewer = Show("```\nx\n```", [new MarkdownCodeBlockAction("Insert at cursor", _ => { })]);

        var button = viewer.Find("button.pmd-code-action");
        Assert.Equal("Insert at cursor", button.TextContent);
        Assert.Equal("button", button.GetAttribute("type"));
        Assert.Null(button.GetAttribute("tabindex"));
    }

    [Fact(DisplayName = "An action with an icon shows the icon, with its label as the tooltip and the accessible name")]
    public void IconAction_ShowsIconWithLabel()
    {
        var viewer = Show("```\nx\n```", [new MarkdownCodeBlockAction("Run", _ => { }) { IconPath = "M8 5v14l11-7z" }]);

        var button = viewer.Find("button.pmd-code-action");
        Assert.Equal("Run", button.GetAttribute("aria-label"));
        Assert.Equal("Run", button.GetAttribute("title"));
        Assert.Equal("true", button.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        Assert.Equal("M8 5v14l11-7z", button.QuerySelector("path")!.GetAttribute("d"));
    }

    [Fact(DisplayName = "Removing the actions removes their buttons")]
    public void ClearActions_RemovesButtons()
    {
        var viewer = Show("```\nx\n```", [new MarkdownCodeBlockAction("Insert", _ => { })]);

        viewer.Render(parameters => parameters.Add(component => component.CodeBlockActions, null));

        Assert.Empty(viewer.FindAll(".pmd-code-action"));
    }
}
