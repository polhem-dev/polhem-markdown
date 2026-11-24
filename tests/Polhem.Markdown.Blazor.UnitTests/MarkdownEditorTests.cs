using Bunit;
using Microsoft.AspNetCore.Components;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Blazor.UnitTests;

public class MarkdownEditorTests : BunitContext
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown-editor.js";

    private readonly BunitJSModuleInterop _module;

    public MarkdownEditorTests()
    {
        _module = JSInterop.SetupModule(ModulePath);
        _module.Mode = JSRuntimeMode.Loose;
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private IRenderedComponent<MarkdownEditor> Edit(string? markdown, Action<string>? changed = null) =>
        Render<MarkdownEditor>(parameters =>
        {
            parameters.Add(editor => editor.Markdown, markdown);
            if (changed is not null)
            {
                parameters.Add(editor => editor.MarkdownChanged, changed);
            }
        });

    /// <summary>The spans the module received so far, applied in order to an empty list.</summary>
    private List<int[]> SentSpans()
    {
        var spans = new List<int[]>();
        foreach (var invocation in _module.Invocations["applySpans"])
        {
            var bytes = Assert.IsType<byte[]>(invocation.Arguments[2]);
            var numbers = new int[bytes.Length / sizeof(int)];
            Buffer.BlockCopy(bytes, 0, numbers, 0, bytes.Length);
            SpanPatchTests.Apply(spans, numbers);
        }
        return spans;
    }

    [Fact(DisplayName = "The editor renders the text in a textarea, over an empty layer for the colors")]
    public void Render_Text_InTextarea()
    {
        var editor = Edit("# Title");

        Assert.Equal("# Title", editor.Find("textarea").GetAttribute("value"));
        Assert.Empty(editor.Find("pre.pmd-editor-layer").Children);
        Assert.Equal("true", editor.Find("pre").GetAttribute("aria-hidden"));
        Assert.False(editor.Find("textarea").HasAttribute("readonly"));
    }

    [Fact(DisplayName = "Rendered statically, the editor shows the text read-only and loads no script")]
    public void Render_Static_ReadOnlyWithoutModule()
    {
        SetRendererInfo(new RendererInfo("Static", isInteractive: false));

        var editor = Edit("text");

        Assert.True(editor.Find("textarea").HasAttribute("readonly"));
        Assert.Empty(JSInterop.Invocations["import"]);
    }

    [Fact(DisplayName = "The module is given the text and a CSS class for every kind of syntax")]
    public void Render_Interactive_InitializesModule()
    {
        Edit("a *b*");

        var init = Assert.Single(_module.Invocations["init"]);
        Assert.Equal("a *b*", init.Arguments[2]);
        var classes = Assert.IsType<string[]>(init.Arguments[3]);
        Assert.Equal("pmd-sx-marker", classes[(int)MarkdownSyntaxKind.Marker]);
        Assert.Equal("pmd-tk-keyword", classes[(int)MarkdownSyntaxKind.CodeKeyword]);
    }

    [Fact(DisplayName = "Every kind of syntax has a CSS class, and plain text and code without a token have none")]
    public void KindClasses_CoverEveryKind()
    {
        foreach (var kind in Enum.GetValues<MarkdownSyntaxKind>())
        {
            var name = SyntaxSpanPatch.KindClass(kind);
            if (kind is MarkdownSyntaxKind.None or MarkdownSyntaxKind.CodeBlock)
            {
                Assert.Equal("", name);
            }
            else
            {
                Assert.StartsWith("pmd-", name, StringComparison.Ordinal);
            }
        }
    }

    [Fact(DisplayName = "The spans of the scan reach the module and cover the syntax of the text")]
    public void Render_Interactive_SendsSpans()
    {
        const string Markdown = "# Title\n\nSome **bold** text.";

        var editor = Edit(Markdown);

        // The scan runs off the render, on a task of its own.
        editor.WaitForAssertion(() => _module.VerifyInvoke("applySpans"));
        var expected = SyntaxSpanPatch.Pack(MarkdownSyntaxScanner.Scan(Markdown));
        Assert.Equal(expected, SentSpans().SelectMany(span => span).ToArray());
    }

    [Fact(DisplayName = "An edit from the textarea changes the text and is handed to the host")]
    public async Task ApplyEdit_RaisesMarkdownChanged()
    {
        string? changed = null;
        var editor = Edit("Hello", text => changed = text);

        await editor.InvokeAsync(() => editor.Instance.ApplyEditAsync(1, 5, 0, " world", more: false));

        Assert.Equal("Hello world", editor.Instance.Text);
        Assert.Equal("Hello world", changed);
    }

    [Fact(DisplayName = "An edit that arrives in parts is handed to the host once, when the last part arrives")]
    public async Task ApplyEdit_InParts_RaisesOnce()
    {
        var changes = new List<string>();
        var editor = Edit("ab", changes.Add);

        await editor.InvokeAsync(() => editor.Instance.ApplyEditAsync(1, 1, 0, "12", more: true));
        await editor.InvokeAsync(() => editor.Instance.ApplyEditAsync(1, 3, 0, "34", more: false));

        Assert.Equal("a1234b", editor.Instance.Text);
        Assert.Equal(["a1234b"], changes);
    }

    [Theory(DisplayName = "An edit that does not fit the text is refused")]
    [InlineData(-1, 0)]
    [InlineData(6, 0)]
    [InlineData(2, 4)]
    [InlineData(0, -1)]
    public async Task ApplyEdit_OutOfRange_Throws(int start, int removed)
    {
        var editor = Edit("Hello");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => editor.InvokeAsync(() => editor.Instance.ApplyEditAsync(1, start, removed, "x", more: false)));
        Assert.Equal("Hello", editor.Instance.Text);
    }

    [Fact(DisplayName = "An edit older than one already taken is refused")]
    public async Task ApplyEdit_OlderVersion_Throws()
    {
        var editor = Edit("ab");
        await editor.InvokeAsync(() => editor.Instance.ApplyEditAsync(5, 0, 0, "x", more: false));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => editor.InvokeAsync(() => editor.Instance.ApplyEditAsync(4, 0, 0, "y", more: false)));
    }

    [Fact(DisplayName = "A text with CRLF line breaks is edited with LF and handed back with CRLF")]
    public async Task ApplyEdit_CrLfText_KeepsCrLf()
    {
        string? changed = null;
        var editor = Edit("a\r\nb", text => changed = text);

        Assert.Equal("a\nb", editor.Find("textarea").GetAttribute("value"));
        await editor.InvokeAsync(() => editor.Instance.ApplyEditAsync(1, 3, 0, "\nc", more: false));

        Assert.Equal("a\r\nb\r\nc", changed);
    }

    [Fact(DisplayName = "The host passing back the text the editor handed it does not replace the text")]
    public async Task SetParameters_OwnText_DoesNotReplace()
    {
        var editor = Edit("ab");
        await editor.InvokeAsync(() => editor.Instance.ApplyEditAsync(1, 2, 0, "c", more: false));

        editor.Render(parameters => parameters.Add(component => component.Markdown, "abc"));

        Assert.Empty(_module.Invocations["setText"]);
    }

    [Fact(DisplayName = "A text the host sets replaces the text in the textarea")]
    public void SetParameters_NewText_Replaces()
    {
        var editor = Edit("old");

        editor.Render(parameters => parameters.Add(component => component.Markdown, "new\r\ntext"));

        var setText = Assert.Single(_module.Invocations["setText"]);
        Assert.Equal("new\ntext", setText.Arguments[1]);
        Assert.Equal("new\ntext", editor.Instance.Text);
    }

    [Fact(DisplayName = "Without word wrap, the textarea does not wrap and the layer is told the same")]
    public void Render_WordWrapOff_NoWrap()
    {
        var editor = Render<MarkdownEditor>(parameters => parameters.Add(component => component.WordWrap, false));

        Assert.Equal("off", editor.Find("textarea").GetAttribute("wrap"));
        Assert.Contains("pmd-editor-nowrap", editor.Find("div").ClassList);
    }

    [Fact(DisplayName = "Attributes the host adds, such as an accessible name, go to the textarea")]
    public void Render_AdditionalAttributes_OnTextarea()
    {
        var editor = Render<MarkdownEditor>(parameters => parameters
            .AddUnmatched("aria-label", "Source")
            .Add(component => component.IsReadOnly, true));

        Assert.Equal("Source", editor.Find("textarea").GetAttribute("aria-label"));
        Assert.True(editor.Find("textarea").HasAttribute("readonly"));
    }

    [Fact(DisplayName = "Undo and redo go to the module and report whether there was a step")]
    public async Task UndoRedo_CallModule()
    {
        _module.Setup<bool>("undo", _ => true).SetResult(true);
        _module.Setup<bool>("redo", _ => true).SetResult(false);
        var editor = Edit("a");

        Assert.True(await editor.InvokeAsync(() => editor.Instance.UndoAsync()));
        Assert.False(await editor.InvokeAsync(() => editor.Instance.RedoAsync()));
    }

    [Fact(DisplayName = "Disposing the editor releases its state in the page")]
    public async Task Dispose_ReleasesModule()
    {
        var editor = Edit("a");

        await editor.Instance.DisposeAsync();

        _module.VerifyInvoke("dispose");
    }
}
