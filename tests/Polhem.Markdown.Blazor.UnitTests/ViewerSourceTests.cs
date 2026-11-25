using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Polhem.Markdown.Tests.Shared;

namespace Polhem.Markdown.Blazor.UnitTests;

public class ViewerSourceTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown.js";

    private readonly BunitJSModuleInterop _module;

    public ViewerSourceTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<MarkdownViewer> Show(string markdown, Action<ComponentParameterCollectionBuilder<MarkdownViewer>>? more = null) =>
        Render<MarkdownViewer>(parameters =>
        {
            parameters.Add(viewer => viewer.Markdown, markdown);
            more?.Invoke(parameters);
        });

    [Fact(DisplayName = "Every top-level block is one element of the viewer, which is how the script finds a block's element")]
    public void Render_AllNodes_OneElementPerTopLevelBlock()
    {
        var viewer = Show(ModelNodeTypes.AllNodesMarkdown());

        Assert.Equal(viewer.Instance.SourceRanges.Count / 2, viewer.Find("div.pmd").Children.Length);
    }

    [Fact(DisplayName = "The source ranges are those of the top-level blocks, in UTF-16 offsets, CJK and CRLF included")]
    public void SourceRanges_TopLevelBlocks()
    {
        var viewer = Show("# 標題\r\n\r\n段落 one\r\n\r\n- a\r\n- b\r\n");

        Assert.Equal([0, 4, 8, 6, 18, 8], viewer.Instance.SourceRanges);
    }

    [Fact(DisplayName = "Without a host for the source features, the viewer loads no script")]
    public void Render_NoCallbacks_DoesNotWatch()
    {
        Show("text");

        Assert.Empty(JSInterop.Invocations["import"]);
    }

    [Fact(DisplayName = "With a host for scrolling or for clicks, the module is given the ranges and what to report")]
    public void Render_WithCallbacks_WatchesSource()
    {
        var viewer = Show("a\n\nb", parameters => parameters
            .Add(component => component.FirstVisibleSourceOffsetChanged, (int _) => { })
            .Add(component => component.OnSourceRequested, (MarkdownSourceRequestedEventArgs _) => { }));

        var watch = Assert.Single(_module.Invocations["watchSource"]);
        Assert.Equal(new[] { 0, 1, 3, 1 }, watch.Arguments[2]);
        Assert.Equal(true, watch.Arguments[3]);
        Assert.Equal(true, watch.Arguments[4]);
    }

    [Fact(DisplayName = "A new text gives the module new ranges; the same text does not call it again")]
    public void Render_NewText_UpdatesRanges()
    {
        var viewer = Show("a", parameters => parameters.Add(component => component.OnSourceRequested, (MarkdownSourceRequestedEventArgs _) => { }));

        viewer.Render(parameters => parameters.Add(component => component.Markdown, "a"));
        viewer.Render(parameters => parameters.Add(component => component.Markdown, "a\n\nb"));

        Assert.Equal(2, _module.Invocations["watchSource"].Count);
        Assert.Equal(new[] { 0, 1, 3, 1 }, _module.Invocations["watchSource"][1].Arguments[2]);
    }

    [Fact(DisplayName = "When the host stops listening, the module stops watching")]
    public void Render_CallbacksRemoved_Unwatches()
    {
        var viewer = Show("a", parameters => parameters.Add(component => component.OnSourceRequested, (MarkdownSourceRequestedEventArgs _) => { }));

        viewer.Render(parameters => parameters.Add(component => component.OnSourceRequested, default(EventCallback<MarkdownSourceRequestedEventArgs>)));

        _module.VerifyInvoke("unwatchSource");
    }

    [Fact(DisplayName = "Scrolling to a source offset goes to the module, which gets the ranges first")]
    public async Task ScrollToSource_CallsModule()
    {
        var viewer = Show("a\n\nb");

        await viewer.InvokeAsync(() => viewer.Instance.ScrollToSourceOffsetAsync(3));

        _module.VerifyInvoke("watchSource");
        Assert.Equal(3, Assert.Single(_module.Invocations["scrollToSource"]).Arguments[1]);
    }

    [Fact(DisplayName = "Scrolling does nothing when rendered statically or with nothing to show")]
    public async Task ScrollToSource_StaticOrEmpty_DoesNothing()
    {
        var empty = Show("");
        await empty.InvokeAsync(() => empty.Instance.ScrollToSourceOffsetAsync(0));
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));
        var stat = Show("a");
        await stat.InvokeAsync(() => stat.Instance.ScrollToSourceOffsetAsync(0));

        Assert.Empty(_module.Invocations["scrollToSource"]);
    }

    [Fact(DisplayName = "The offset at the top of the view reaches the host once per change, kept within the text")]
    public async Task FirstVisibleChanged_RaisesCallback()
    {
        var offsets = new List<int>();
        var viewer = Show("abc", parameters => parameters.Add(component => component.FirstVisibleSourceOffsetChanged, offsets.Add));
        var bridge = new MarkdownViewerBridge(viewer.Instance);

        await viewer.InvokeAsync(() => bridge.FirstVisibleChanged(2));
        await viewer.InvokeAsync(() => bridge.FirstVisibleChanged(2));
        await viewer.InvokeAsync(() => bridge.FirstVisibleChanged(99));

        Assert.Equal([2, 3], offsets);
        Assert.Equal(3, viewer.Instance.FirstVisibleSourceOffset);
    }

    [Fact(DisplayName = "A click with the command key reaches the host with its source offset, kept within the text")]
    public async Task SourceRequested_RaisesCallback()
    {
        var offsets = new List<int>();
        var viewer = Show("abc", parameters => parameters.Add(component => component.OnSourceRequested, (MarkdownSourceRequestedEventArgs e) => offsets.Add(e.SourceOffset)));
        var bridge = new MarkdownViewerBridge(viewer.Instance);

        await viewer.InvokeAsync(() => bridge.SourceRequested(1));
        await viewer.InvokeAsync(() => bridge.SourceRequested(-5));

        Assert.Equal([1, 0], offsets);
        Assert.Throws<ArgumentOutOfRangeException>(() => new MarkdownSourceRequestedEventArgs(-1));
    }

    [Theory(DisplayName = "A script call that fails does not end the circuit")]
    [MemberData(nameof(MarkdownEditorTests.ScriptFailures), MemberType = typeof(MarkdownEditorTests))]
    public async Task ScriptFails_IsIgnored(Exception failure)
    {
        _module.SetupVoid("watchSource", _ => true).SetException(failure);
        _module.SetupVoid("unwatchSource", _ => true).SetException(failure);
        var viewer = Show("a", parameters => parameters.Add(component => component.OnSourceRequested, (MarkdownSourceRequestedEventArgs _) => { }));

        await viewer.InvokeAsync(() => viewer.Instance.ScrollToSourceOffsetAsync(0));
        await viewer.Instance.DisposeAsync();

        Assert.Empty(_module.Invocations["scrollToSource"]);
    }

    [Theory(DisplayName = "A scroll call that fails does not end the circuit")]
    [MemberData(nameof(MarkdownEditorTests.ScriptFailures), MemberType = typeof(MarkdownEditorTests))]
    public async Task ScrollFails_IsIgnored(Exception failure)
    {
        _module.SetupVoid("scrollToSource", _ => true).SetException(failure);
        var viewer = Show("a");

        await viewer.InvokeAsync(() => viewer.Instance.ScrollToSourceOffsetAsync(0));

        _module.VerifyInvoke("scrollToSource");
    }

    [Fact(DisplayName = "Disposing a viewer that watched the source stops watching")]
    public async Task Dispose_Unwatches()
    {
        var viewer = Show("a", parameters => parameters.Add(component => component.FirstVisibleSourceOffsetChanged, (int _) => { }));

        await viewer.Instance.DisposeAsync();

        _module.VerifyInvoke("unwatchSource");
    }
}
