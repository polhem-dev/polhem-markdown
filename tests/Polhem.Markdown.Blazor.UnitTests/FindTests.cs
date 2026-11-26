using Bunit;
using Microsoft.AspNetCore.Components;

namespace Polhem.Markdown.Blazor.UnitTests;

/// <summary>Finding text in the viewer from code (polhem-dev/polhem-markdown#85).</summary>
public class FindTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown.js";

    private readonly BunitJSModuleInterop _module;

    public FindTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<MarkdownViewer> Show() =>
        Render<MarkdownViewer>(parameters => parameters.Add(viewer => viewer.Markdown, "one and one"));

    [Fact(DisplayName = "Find asks the script to find the text in the viewer's root, and keeps the count and the current match")]
    public async Task Find_CallsScript()
    {
        _module.Setup<int[]>("find", _ => true).SetResult([2, 1]);
        var viewer = Show();

        var count = await viewer.InvokeAsync(() => viewer.Instance.FindAsync("one", matchCase: true));

        Assert.Equal(2, count);
        Assert.Equal(1, viewer.Instance.CurrentFindMatch);
        var call = Assert.Single(_module.Invocations["find"]);
        Assert.IsType<ElementReference>(call.Arguments[0]);
        Assert.Equal("one", call.Arguments[1]);
        Assert.Equal(true, call.Arguments[2]);
    }

    [Fact(DisplayName = "Next and previous step through the matches by one, and clearing forgets them")]
    public async Task Step_And_Clear()
    {
        _module.Setup<int[]>("find", _ => true).SetResult([2, 1]);
        _module.Setup<int[]>("findStep", invocation => (int)invocation.Arguments[1]! == 1).SetResult([2, 2]);
        _module.Setup<int[]>("findStep", invocation => (int)invocation.Arguments[1]! == -1).SetResult([2, 1]);
        _module.SetupVoid("clearFind", _ => true).SetVoidResult();
        var viewer = Show();
        await viewer.InvokeAsync(() => viewer.Instance.FindAsync("one"));

        Assert.True(await viewer.InvokeAsync(viewer.Instance.FindNextAsync));
        Assert.Equal(2, viewer.Instance.CurrentFindMatch);
        Assert.True(await viewer.InvokeAsync(viewer.Instance.FindPreviousAsync));
        Assert.Equal(1, viewer.Instance.CurrentFindMatch);

        await viewer.InvokeAsync(viewer.Instance.ClearFindAsync);
        Assert.Equal(0, viewer.Instance.FindMatchCount);
        Assert.Single(_module.Invocations["clearFind"]);
    }

    [Fact(DisplayName = "Rendered without interactivity, find does nothing and finds nothing")]
    public async Task Static_FindsNothing()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var viewer = Show();

        Assert.Equal(0, await viewer.InvokeAsync(() => viewer.Instance.FindAsync("one")));
        Assert.Empty(JSInterop.Invocations);
    }
}
