using Microsoft.JSInterop;

namespace Polhem.Markdown.Blazor;

/// <summary>
/// What the script of a <see cref="MarkdownEditor"/> calls, kept off the editor so the call it needs to be public for
/// is not part of the editor's API.
/// </summary>
internal sealed class MarkdownEditorBridge(MarkdownEditor editor)
{
    /// <summary>Takes an edit of the textarea; see <see cref="MarkdownEditor.ApplyEditAsync"/>.</summary>
    [JSInvokable]
    public Task ApplyEdit(int version, int start, int removed, string text, bool more) =>
        editor.ApplyEditAsync(version, start, removed, text, more);
}
