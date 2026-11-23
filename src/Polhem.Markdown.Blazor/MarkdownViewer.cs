using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

/// <summary>
/// Shows a Markdown text as semantic HTML. It renders the same model as the Avalonia viewer of Polhem.Markdown, so both
/// front ends show a text the same way, and applies the same safety policy: raw HTML shows as text, images as their
/// alternative text, and only links whose scheme is in <see cref="Options"/> become links.
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
    // Keyed by heading id; string keys compare ordinally by default.
    private readonly Dictionary<string, ElementReference> _headings = [];
    private IJSObjectReference? _module;

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

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        if (!string.Equals(Markdown, _parsedMarkdown, StringComparison.Ordinal) || !ReferenceEquals(Options, _parsedOptions))
        {
            _document = string.IsNullOrEmpty(Markdown) ? s_empty : MarkdownModelBuilder.Build(Markdown, Options ?? new MarkdownOptions());
            _parsedMarkdown = Markdown;
            _parsedOptions = Options;
        }
    }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var tree = new MarkdownRenderTree(this, builder);
        tree.Document(_document, Class);
        RenderedNodeTypes = tree.RenderedNodeTypes;
        RenderedShapeTypes = tree.RenderedShapeTypes;
    }

    internal bool HandlesLinks => OnLinkActivated.HasDelegate;

    internal bool IsCopied(CodeBlockNode block) => ReferenceEquals(block, _copiedBlock);

    /// <summary>Records the element of the heading with <paramref name="id"/>, as Blazor captures it.</summary>
    internal void SetHeadingElement(string id, ElementReference element) => _headings[id] = element;

    /// <summary>
    /// Scrolls the heading with <paramref name="id"/> to the top of the view, as GitHub does and as the Avalonia viewer
    /// does, and moves focus to it so a screen reader follows.
    /// </summary>
    internal async Task FocusHeadingAsync(string id)
    {
        if (!_headings.TryGetValue(id, out var element))
        {
            return;
        }
        try
        {
            // NOTE: `ElementReference.FocusAsync` alone does not scroll when the heading already has focus, as it does
            // after a second click on the same link, and Blazor offers no call that scrolls an element, so the module
            // does both.
            _module ??= await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            await _module.InvokeVoidAsync("scrollToHeading", element);
        }
        catch (JSException)
        {
            // The heading left the page between the click and the call, as a text that is still arriving is
            // re-rendered, or the module could not load. The click then does nothing.
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
        if (_module is null)
        {
            return;
        }
        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
            // The circuit is already gone, and the module with it.
        }
        _module = null;
    }
}
