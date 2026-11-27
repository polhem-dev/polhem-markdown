using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

/// <summary>
/// One top-level block of a <see cref="MarkdownViewer"/>. It renders again only when its key or the viewer's settings
/// change, when the code block whose copy button shows a check mark is in it, or when the host's code block actions
/// change and it has a code block, so an edit or a reply that grows renders
/// the blocks it changes and leaves the rest alone, as the Avalonia viewer keeps their controls.
/// </summary>
internal sealed class MarkdownBlock : ComponentBase, IDisposable
{
    private BlockNode? _shown;
    private string? _shownKey;
    private int _shownGeneration;
    private CodeBlockNode? _shownCopied;
    private int _shownActionsGeneration;
    private bool _hasCode;
    private bool _changed;

    /// <summary>The viewer the block is in.</summary>
    [Parameter]
    public MarkdownViewer Viewer { get; set; } = default!;

    /// <summary>The block in the text the viewer parsed last.</summary>
    [Parameter]
    public BlockNode Block { get; set; } = default!;

    /// <summary>The block's source and what it shows from the rest of the text (see <see cref="MarkdownViewer"/>).</summary>
    [Parameter]
    public string Key { get; set; } = "";

    /// <summary>Changes when a setting of the viewer that every block shows changes.</summary>
    [Parameter]
    public int Generation { get; set; }

    /// <summary>The code block whose copy button shows a check mark, if any, in this block or another.</summary>
    [Parameter]
    public CodeBlockNode? Copied { get; set; }

    /// <summary>Changes when the host's code block actions, or which blocks show them, may have changed.</summary>
    [Parameter]
    public int ActionsGeneration { get; set; }

    /// <summary>The node types this block drew when it last rendered.</summary>
    public IReadOnlySet<Type> RenderedNodeTypes { get; private set; } = new HashSet<Type>();

    /// <summary>The diagram shape types this block drew when it last rendered.</summary>
    public IReadOnlySet<Type> RenderedShapeTypes { get; private set; } = new HashSet<Type>();

    /// <summary>The formula part types this block wrote when it last rendered.</summary>
    public IReadOnlySet<Type> RenderedFormulaTypes { get; private set; } = new HashSet<Type>();

    /// <inheritdoc/>
    protected override void OnInitialized() => Viewer.AddBlock(this);

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        // NOTE: a kept block goes on rendering the node it was built from, not the equal one the new text parsed into.
        // The handlers it wrote hold that node, and the copy button finds its check mark by the node's identity.
        _changed = _shown is null || !string.Equals(Key, _shownKey, StringComparison.Ordinal) || Generation != _shownGeneration;
        if (_changed)
        {
            _shown = Block;
            _shownKey = Key;
            _shownGeneration = Generation;
            _hasCode = NodeTree.SelfAndDescendants(_shown).Any(node => node is CodeBlockNode);
        }
        else if (!ReferenceEquals(Copied, _shownCopied))
        {
            _changed = Holds(_shownCopied) || Holds(Copied);
        }
        _changed |= _hasCode && ActionsGeneration != _shownActionsGeneration;
        _shownCopied = Copied;
        _shownActionsGeneration = ActionsGeneration;
    }

    /// <inheritdoc/>
    protected override bool ShouldRender() => _changed;

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var tree = new MarkdownRenderTree(Viewer, builder);
        tree.TopLevelBlock(_shown!);
        RenderedNodeTypes = tree.RenderedNodeTypes;
        RenderedShapeTypes = tree.RenderedShapeTypes;
        RenderedFormulaTypes = tree.RenderedFormulaTypes;
        Viewer.BlockRenderCount++;
    }

    /// <inheritdoc/>
    public void Dispose() => Viewer.RemoveBlock(this);

    private bool Holds(CodeBlockNode? code) =>
        code is not null && NodeTree.SelfAndDescendants(_shown!).Any(node => ReferenceEquals(node, code));
}
