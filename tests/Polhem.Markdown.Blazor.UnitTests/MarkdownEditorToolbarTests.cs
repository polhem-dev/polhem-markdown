using Bunit;
using Microsoft.AspNetCore.Components;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Blazor.UnitTests;

public class MarkdownEditorToolbarTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown-editor.js";

    private readonly BunitJSModuleInterop _module;

    public MarkdownEditorToolbarTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<MarkdownEditor> Edit(Action<ComponentParameterCollectionBuilder<MarkdownEditor>>? parameters = null) =>
        Render<MarkdownEditor>(builder =>
        {
            builder.Add(editor => editor.Markdown, "title");
            parameters?.Invoke(builder);
        });

    private static IEnumerable<AngleSharp.Dom.IElement> Buttons(IRenderedComponent<MarkdownEditor> editor) =>
        editor.FindAll(".pmd-editor-toolbar > button");

    private static AngleSharp.Dom.IElement Button(IRenderedComponent<MarkdownEditor> editor, MarkdownToolbarItem item) =>
        editor.Find($".pmd-editor-toolbar > button[data-pmd-item={item}]");

    [Fact(DisplayName = "The toolbar shows above the text, with a button for every item in the shared order")]
    public void Toolbar_ShowsEveryItemAboveText()
    {
        var editor = Edit();

        var items = Buttons(editor).Select(button => button.GetAttribute("data-pmd-item")!).ToList();
        var expected = ToolbarItems.Groups.SelectMany(group => group).Select(item => item.ToString()).ToList();
        Assert.Equal(expected, items);
        Assert.Equal(ToolbarItems.Groups.Count - 1, editor.FindAll(".pmd-editor-toolbar > .pmd-editor-separator").Count);
        Assert.Equal("toolbar", editor.Find(".pmd-editor-toolbar").GetAttribute("role"));

        var root = editor.Find("div.pmd-editor");
        Assert.Equal(["pmd-editor-toolbar", "pmd-editor-body"], root.Children.Select(child => child.ClassName));
    }

    [Fact(DisplayName = "Each button draws its icon, and only the first is a tab stop")]
    public void Buttons_HaveIconsAndOneTabStop()
    {
        var editor = Edit();

        Assert.All(Buttons(editor), button =>
        {
            Assert.Equal("button", button.GetAttribute("type"));
            var item = Enum.Parse<MarkdownToolbarItem>(button.GetAttribute("data-pmd-item")!);
            Assert.Equal(ToolbarItems.Icon(item), button.QuerySelector("svg > path")!.GetAttribute("d"));
            Assert.Equal("true", button.QuerySelector("svg")!.GetAttribute("aria-hidden"));
        });
        Assert.Equal(["Bold"], Buttons(editor).Where(button => button.GetAttribute("tabindex") == "0").Select(button => button.GetAttribute("data-pmd-item")));
    }

    [Fact(DisplayName = "Without the toolbar only the text is rendered")]
    public void ShowToolbar_False_RendersNoToolbar()
    {
        var editor = Edit(parameters => parameters.Add(component => component.ShowToolbar, false));

        Assert.Empty(editor.FindAll(".pmd-editor-toolbar"));
        Assert.Single(editor.FindAll("textarea"));
    }

    [Fact(DisplayName = "A button is named by its label, and its tooltips add the shortcut for either platform")]
    public void Labels_DefaultToEnglishWithShortcut()
    {
        var editor = Edit();
        var bold = Button(editor, MarkdownToolbarItem.Bold);

        Assert.Equal("Bold", bold.GetAttribute("aria-label"));
        Assert.Equal("Bold (Ctrl+B)", bold.GetAttribute("title"));
        Assert.Equal("Bold (⌘B)", bold.GetAttribute("data-pmd-title-mac"));
        Assert.Equal("Table", Button(editor, MarkdownToolbarItem.Table).GetAttribute("title"));
    }

    [Fact(DisplayName = "The host's labels replace the English ones, and null keeps the English one")]
    public void ToolbarLabels_ReplaceLabels()
    {
        var editor = Edit(parameters =>
            parameters.Add(component => component.ToolbarLabels, item => item == MarkdownToolbarItem.Bold ? "粗體" : null));

        var bold = Button(editor, MarkdownToolbarItem.Bold);
        Assert.Equal("粗體", bold.GetAttribute("aria-label"));
        Assert.Equal("粗體 (Ctrl+B)", bold.GetAttribute("title"));
        Assert.Equal("Italic", Button(editor, MarkdownToolbarItem.Italic).GetAttribute("aria-label"));
    }

    [Fact(DisplayName = "The format buttons are enabled, and undo and redo wait for the module to enable them")]
    public void Buttons_EnabledButHistory()
    {
        var editor = Edit();

        Assert.False(Button(editor, MarkdownToolbarItem.Bold).HasAttribute("disabled"));
        Assert.False(Button(editor, MarkdownToolbarItem.Heading).HasAttribute("disabled"));
        Assert.True(Button(editor, MarkdownToolbarItem.Undo).HasAttribute("disabled"));
        Assert.True(Button(editor, MarkdownToolbarItem.Redo).HasAttribute("disabled"));
    }

    [Fact(DisplayName = "Every button is disabled while the editor is read-only")]
    public void ReadOnly_DisablesButtons()
    {
        var editor = Edit(parameters => parameters.Add(component => component.IsReadOnly, true));

        Assert.All(Buttons(editor), button => Assert.True(button.HasAttribute("disabled")));
    }

    [Fact(DisplayName = "Rendered statically, the toolbar shows with every button disabled")]
    public void Static_DisablesButtons()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        var editor = Edit();

        Assert.All(Buttons(editor), button => Assert.True(button.HasAttribute("disabled")));
    }

    [Fact(DisplayName = "The heading cycle comes back as the formatter's edit")]
    public void Command_CycleHeading_ReturnsEdit()
    {
        var editor = Edit();

        Assert.Equal([0, 5, "# title", 2, 0], editor.Instance.Command("cycleHeading", 0, 0, 0, null));
    }

    [Fact(DisplayName = "The host's heading cycle goes to the module, as the button's does")]
    public async Task CycleHeadingAsync_CallsModule()
    {
        var editor = Edit();

        await editor.InvokeAsync(editor.Instance.CycleHeadingAsync);

        Assert.Single(_module.Invocations["cycleHeading"]);
    }

    [Fact(DisplayName = "A toolbar shown after the editor started is set up by the module")]
    public void ShowToolbar_Later_RefreshesToolbar()
    {
        var editor = Edit(parameters => parameters.Add(component => component.ShowToolbar, false));
        Assert.Empty(_module.Invocations["refreshToolbar"]);

        editor.Render(parameters => parameters.Add(component => component.ShowToolbar, true));

        Assert.Single(_module.Invocations["refreshToolbar"]);
        Assert.Single(editor.FindAll(".pmd-editor-toolbar"));
    }
}
