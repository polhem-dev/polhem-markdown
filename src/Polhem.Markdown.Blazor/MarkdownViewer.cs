using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

/// <summary>
/// Shows a Markdown text as semantic HTML. It renders the same model as the Avalonia viewer of Polhem.Markdown, so both
/// front ends show a text the same way, and applies the same safety policy: raw HTML shows as text, only images and links
/// whose scheme is in <see cref="Options"/> load or become links, and other images show their alternative text.
/// </summary>
/// <remarks>
/// The markup is built with a render tree, never from an HTML string, so no text reaches the page as markup. Styles are
/// in <c>_content/Polhem.Markdown.Blazor/polhem-markdown.css</c>, which the host page links; they inherit the font and
/// colors of the surrounding page, and the <c>--pmd-*</c> CSS variables listed in the package README set the rest. A
/// link to a heading of the text loads the package's <c>polhem-markdown.js</c> module on its first click; the page does
/// not link it.
/// </remarks>
public class MarkdownViewer : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown.js";

    private static readonly DocumentNode s_empty = new([]);

    private DocumentNode _document = s_empty;
    private string? _parsedMarkdown;
    private MarkdownOptions? _parsedOptions;
    private CodeBlockNode? _copiedBlock;
    // Keyed by anchor id; string keys compare ordinally by default.
    private readonly Dictionary<string, ElementReference> _anchors = [];
    private Task<IJSObjectReference>? _module;

    /// <summary>Gets or sets the Markdown text to show.</summary>
    [Parameter]
    public string? Markdown { get; set; }

    /// <summary>
    /// Gets or sets the options, such as the link schemes that become links. <see langword="null"/> uses the defaults of
    /// <see cref="MarkdownOptions"/>. Changing the set inside the same instance takes effect on the next change of
    /// <see cref="Markdown"/>; pass a new instance to apply it at once.
    /// </summary>
    [Parameter]
    public MarkdownOptions? Options { get; set; }

    /// <summary>
    /// Gets or sets what happens when a user clicks a link. When it is set, the click does not navigate and the host
    /// decides what the link does; when it is not, the browser opens the link in a new tab.
    /// </summary>
    [Parameter]
    public EventCallback<MarkdownLinkActivatedEventArgs> OnLinkActivated { get; set; }

    /// <summary>
    /// Gets or sets the accessible name and tooltip of the copy button of a code block. Set it to translate the label.
    /// </summary>
    [Parameter]
    public string CopyCodeLabel { get; set; } = "Copy";

    /// <summary>Gets or sets further CSS classes for the outer element.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>The node types the last render drew, for the node coverage test.</summary>
    internal IReadOnlySet<Type> RenderedNodeTypes { get; private set; } = new HashSet<Type>();

    /// <summary>The diagram shape types the last render drew, for the shape coverage test.</summary>
    internal IReadOnlySet<Type> RenderedShapeTypes { get; private set; } = new HashSet<Type>();

    /// <summary>The formula part types the last render wrote, for the formula coverage test.</summary>
    internal IReadOnlySet<Type> RenderedFormulaTypes { get; private set; } = new HashSet<Type>();

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private IServiceProvider Services { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (!string.Equals(Markdown, _parsedMarkdown, StringComparison.Ordinal) || !ReferenceEquals(Options, _parsedOptions))
        {
            _document = string.IsNullOrEmpty(Markdown) ? s_empty : MarkdownModelBuilder.Build(Markdown, Options ?? new MarkdownOptions());
            _parsedMarkdown = Markdown;
            _parsedOptions = Options;
            ForgetRemovedAnchors();
        }
    }

    /// <summary>
    /// Drops the elements of anchors the text no longer has. While a heading arrives its id changes with every chunk
    /// (<c>u</c>, <c>us</c>, <c>usa</c>), and each id would otherwise stay for the life of the viewer.
    /// </summary>
    private void ForgetRemovedAnchors()
    {
        if (_anchors.Count == 0)
        {
            return;
        }
        var ids = _document.Blocks
            .SelectMany(NodeTree.SelfAndDescendants)
            .Select(NodeTree.AnchorId)
            .OfType<string>()
            .ToHashSet(StringComparer.Ordinal);
        foreach (var id in _anchors.Keys.Where(id => !ids.Contains(id)).ToList())
        {
            _anchors.Remove(id);
        }
    }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var tree = new MarkdownRenderTree(this, builder);
        tree.Document(_document, Class);
        RenderedNodeTypes = tree.RenderedNodeTypes;
        RenderedShapeTypes = tree.RenderedShapeTypes;
        RenderedFormulaTypes = tree.RenderedFormulaTypes;
    }

    internal bool HandlesLinks => OnLinkActivated.HasDelegate;

    internal bool IsCopied(CodeBlockNode block) => ReferenceEquals(block, _copiedBlock);

    /// <summary>
    /// The href of a link to the anchor with <paramref name="id"/>: the current page with the id as its fragment.
    /// </summary>
    /// <remarks>
    /// NOTE: a bare <c>#id</c> resolves against a <c>base</c> element, which usually names the site root, so before the
    /// component is interactive (static rendering, or prerendered output before its circuit connects) a click, and a
    /// click that opens a new tab at any time, would go to another page. Rendering outside a page, as with
    /// <c>HtmlRenderer</c>, has no current page, and the bare fragment is all there is.
    /// </remarks>
    internal string AnchorHref(string id)
    {
        try
        {
            if (Services.GetService(typeof(NavigationManager)) is NavigationManager navigation)
            {
                // Relative to the base, the page resolves to itself without naming the host, which during prerendering
                // is the one the server saw and may be an internal one behind a proxy.
                var page = navigation.ToBaseRelativePath(navigation.Uri);
                var hash = page.IndexOf('#', StringComparison.Ordinal);
                return (hash < 0 ? page : page[..hash]) + "#" + id;
            }
        }
        catch (InvalidOperationException)
        {
            // The navigation manager exists but was never initialized, as outside a page.
        }
        return "#" + id;
    }

    /// <summary>
    /// Records the element of the anchor with <paramref name="id"/> (a heading, a footnote, or a reference to one), as
    /// Blazor captures it.
    /// </summary>
    internal void SetAnchorElement(string id, ElementReference element) => _anchors[id] = element;

    /// <summary>
    /// Scrolls the anchor with <paramref name="id"/> to the top of the view, as GitHub does and as the Avalonia viewer
    /// does, and moves focus to it so a screen reader follows.
    /// </summary>
    internal async Task MoveToAnchorAsync(string id)
    {
        if (!_anchors.TryGetValue(id, out var element))
        {
            return;
        }
        try
        {
            // NOTE: `ElementReference.FocusAsync` alone does not scroll when the target already has focus, as it does
            // after a second click on the same link, and Blazor offers no call that scrolls an element, so the module
            // does both.
            // The task, not its result, is kept, so two quick clicks import the module once.
            _module ??= JS.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
            var module = await _module;
            await module.InvokeVoidAsync("scrollToAnchor", element);
        }
        catch (JSException)
        {
            // The heading left the page between the click and the call, as a text that is still arriving is
            // re-rendered, or the module could not load. The click then does nothing.
            ForgetFailedImport();
        }
        catch (TaskCanceledException)
        {
            // The call timed out on a slow connection. Left unhandled, it would end the circuit over a click.
            ForgetFailedImport();
        }
    }

    /// <summary>Drops an import that failed, so the next click tries again instead of failing the same way.</summary>
    private void ForgetFailedImport()
    {
        if (_module is { IsCompleted: true, IsCompletedSuccessfully: false })
        {
            _module = null;
        }
    }

    internal Task ActivateLinkAsync(Uri uri) => OnLinkActivated.InvokeAsync(new MarkdownLinkActivatedEventArgs(uri));

    /// <summary>Puts the code on the clipboard and shows a check mark on the button for a moment.</summary>
    internal async Task CopyAsync(CodeBlockNode block)
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", block.Code);
        }
        catch (JSException)
        {
            // The browser refuses the clipboard outside a secure context or without permission. The button then
            // simply shows no check mark; the user can still select the code.
            return;
        }

        _copiedBlock = block;
        StateHasChanged();
        await Task.Delay(TimeSpan.FromSeconds(1.5));
        if (ReferenceEquals(_copiedBlock, block))
        {
            _copiedBlock = null;
            StateHasChanged();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the script module, if a link to a heading loaded it.</summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        var module = _module;
        _module = null;
        if (module is null)
        {
            return;
        }
        try
        {
            await (await module).DisposeAsync();
        }
        catch (JSException)
        {
            // The import itself failed, so there is nothing to release.
        }
        catch (JSDisconnectedException)
        {
            // The circuit is already gone, and the module with it.
        }
        catch (TaskCanceledException)
        {
            // The import never finished.
        }
    }
}
