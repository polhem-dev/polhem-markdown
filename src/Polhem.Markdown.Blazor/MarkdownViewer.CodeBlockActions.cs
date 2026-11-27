using Microsoft.AspNetCore.Components;

namespace Polhem.Markdown.Blazor;

// A host's own buttons on code blocks (polhem-dev/polhem-markdown#99).
public partial class MarkdownViewer
{
    private IReadOnlyList<MarkdownCodeBlockAction>? _shownActions;
    private int _actionsGeneration;

    /// <summary>
    /// Gets or sets the host's own buttons on code blocks, shown in this order before the copy button, or
    /// <see langword="null"/> for none.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each action's <see cref="MarkdownCodeBlockAction.IsVisible"/> is asked when a block renders: when the text or this
    /// list changes, and when <see cref="RefreshCodeBlockActionsAsync"/> is called. A new list renders every block with a
    /// code block again, so a host that keeps its list in a field, rather than building one in its markup, saves that work
    /// when it renders. A diagram or a formula written as a code block has no buttons, as it has no copy button.
    /// </para>
    /// <para>
    /// An action runs on the renderer's dispatcher, in WebAssembly and on the server alike. When it changes what the host
    /// shows, the host renders itself again, as with any delegate that is not an <see cref="EventCallback"/>.
    /// </para>
    /// </remarks>
    [Parameter]
    public IReadOnlyList<MarkdownCodeBlockAction>? CodeBlockActions { get; set; }

    /// <summary>
    /// Asks each action's <see cref="MarkdownCodeBlockAction.IsVisible"/> again for every code block shown, for when what
    /// it depends on has changed, such as whether an editor has the focus. It renders only the blocks that have a code
    /// block.
    /// </summary>
    /// <returns>A task that completes when the viewer has rendered.</returns>
    public Task RefreshCodeBlockActionsAsync() => InvokeAsync(() =>
    {
        _actionsGeneration++;
        StateHasChanged();
    });

    /// <summary>A number that changes when the blocks with a code block must ask the actions again.</summary>
    private int CodeBlockActionsGeneration()
    {
        if (!ReferenceEquals(CodeBlockActions, _shownActions))
        {
            _shownActions = CodeBlockActions;
            _actionsGeneration++;
        }
        return _actionsGeneration;
    }
}
