using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Blazor;

// The toolbar above the text: a button for each format, the heading cycle, undo and redo, in the groups and order of
// ToolbarItems, as on the Avalonia editor. Blazor renders the buttons; the script module handles their clicks and keys,
// so a click keeps the focus and the selection in the textarea, and enables undo and redo, whose history it keeps.
public partial class MarkdownEditor
{
    /// <summary>Whether the toolbar was shown when the module last set it up, to set up a toolbar shown since.</summary>
    private bool _toolbarReady;

    /// <summary>Gets or sets whether the toolbar shows above the text. The default is <see langword="true"/>.</summary>
    /// <remarks>
    /// The toolbar has a button for each <see cref="MarkdownToolbarItem"/>. A host that lays out buttons of its own sets
    /// this to <see langword="false"/> and calls <see cref="ApplyFormatAsync"/>, <see cref="CycleHeadingAsync"/>,
    /// <see cref="UndoAsync"/> and <see cref="RedoAsync"/> from them. Rendered statically, the buttons are disabled.
    /// </remarks>
    [Parameter]
    public bool ShowToolbar { get; set; } = true;

    /// <summary>
    /// Gets or sets the labels of the toolbar's buttons: a function that returns the label of a button, or
    /// <see langword="null"/> to keep the English one. The default, <see langword="null"/>, keeps all the English labels.
    /// </summary>
    /// <remarks>
    /// A label is the button's tooltip and its accessible name. The tooltip adds the button's shortcut, written for the
    /// platform the page runs on, so a label does not need to.
    /// </remarks>
    [Parameter]
    public Func<MarkdownToolbarItem, string?>? ToolbarLabels { get; set; }

    /// <summary>
    /// Makes the lines the selection touches headings one level deeper than the first of them is, as one step to undo:
    /// plain text becomes a level 1 heading, then level 2, then level 3, and a heading of level 3 or deeper becomes plain
    /// text again. Nothing happens when the editor is read-only.
    /// </summary>
    /// <remarks>Lines without text are left as they are.</remarks>
    /// <returns>A task that completes when the request has reached the page; the edit follows from there.</returns>
    public async Task CycleHeadingAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("cycleHeading", _root);
        }
    }

    private void BuildToolbar(RenderTreeBuilder builder)
    {
        var enabled = RendererInfo.IsInteractive && !IsReadOnly;
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", "pmd-editor-toolbar");
        builder.AddAttribute(2, "role", "toolbar");
        builder.AddAttribute(3, "aria-label", "Formatting");
        var first = true;
        foreach (var group in ToolbarItems.Groups)
        {
            if (!first)
            {
                builder.OpenElement(4, "span");
                builder.AddAttribute(5, "class", "pmd-editor-separator");
                builder.AddAttribute(6, "role", "separator");
                builder.CloseElement();
            }
            foreach (var item in group)
            {
                BuildButton(builder, item, tabStop: first && item == group[0], enabled);
            }
            first = false;
        }
        builder.CloseElement();
    }

    /// <remarks>
    /// Undo and redo start disabled, and the module enables them once there is a step to take. Blazor leaves an attribute
    /// alone while its own value does not change, so a render does not undo what the module set.
    /// </remarks>
    private void BuildButton(RenderTreeBuilder builder, MarkdownToolbarItem item, bool tabStop, bool enabled)
    {
        var label = ToolbarLabels?.Invoke(item) ?? ToolbarItems.Label(item);
        var history = item is MarkdownToolbarItem.Undo or MarkdownToolbarItem.Redo;
        builder.OpenElement(10, "button");
        builder.SetKey(item);
        builder.AddAttribute(11, "type", "button");
        builder.AddAttribute(12, "class", "pmd-editor-button");
        builder.AddAttribute(13, "data-pmd-item", item.ToString());
        builder.AddAttribute(14, "tabindex", tabStop ? "0" : "-1");
        builder.AddAttribute(15, "aria-label", label);
        builder.AddAttribute(16, "title", ToolbarItems.Tooltip(item, label, mac: false));
        builder.AddAttribute(17, "data-pmd-title-mac", ToolbarItems.Tooltip(item, label, mac: true));
        builder.AddAttribute(18, "disabled", history || !enabled);

        builder.OpenElement(19, "svg");
        builder.AddAttribute(20, "viewBox", "0 0 16 16");
        builder.AddAttribute(21, "width", "16");
        builder.AddAttribute(22, "height", "16");
        builder.AddAttribute(23, "aria-hidden", "true");
        builder.OpenElement(24, "path");
        builder.AddAttribute(25, "d", ToolbarItems.Icon(item));
        builder.CloseElement();
        builder.CloseElement();

        builder.CloseElement();
    }

    /// <summary>
    /// Has the module set up a toolbar shown since it last did: the tooltips for the platform, and undo and redo for the
    /// history.
    /// </summary>
    private async Task RefreshToolbarAsync()
    {
        if (_module is null || ShowToolbar == _toolbarReady)
        {
            return;
        }
        _toolbarReady = ShowToolbar;
        if (ShowToolbar)
        {
            await CallModuleAsync("refreshToolbar", _root);
        }
    }
}
