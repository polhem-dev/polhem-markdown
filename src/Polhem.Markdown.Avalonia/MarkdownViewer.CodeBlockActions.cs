using Avalonia;
using Avalonia.LogicalTree;

namespace Polhem.Markdown.Avalonia;

// A host's own buttons on code blocks (polhem-dev/polhem-markdown#99).
public partial class MarkdownViewer
{
    /// <summary>Defines the <see cref="CodeBlockActions"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<MarkdownCodeBlockAction>?> CodeBlockActionsProperty =
        AvaloniaProperty.Register<MarkdownViewer, IReadOnlyList<MarkdownCodeBlockAction>?>(nameof(CodeBlockActions));

    /// <summary>
    /// Gets or sets the host's own buttons on code blocks, shown in this order before the copy button, or
    /// <see langword="null"/> for none. Setting it updates the blocks shown without building them again.
    /// </summary>
    /// <remarks>
    /// Each action's <see cref="MarkdownCodeBlockAction.IsVisible"/> is asked when a block is built and when
    /// <see cref="RefreshCodeBlockActions"/> is called. A diagram or a formula written as a code block has no buttons, as it
    /// has no copy button.
    /// </remarks>
    public IReadOnlyList<MarkdownCodeBlockAction>? CodeBlockActions
    {
        get => GetValue(CodeBlockActionsProperty);
        set => SetValue(CodeBlockActionsProperty, value);
    }

    /// <summary>
    /// Asks each action's <see cref="MarkdownCodeBlockAction.IsVisible"/> again for every code block shown, for when what
    /// it depends on has changed, such as whether an editor has the focus.
    /// </summary>
    public void RefreshCodeBlockActions()
    {
        foreach (var code in _panel.GetLogicalDescendants().OfType<CodeBlockView>())
        {
            code.UpdateActions();
        }
    }

    private void OnCodeBlockActionsPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == CodeBlockActionsProperty)
        {
            RefreshCodeBlockActions();
        }
    }
}
