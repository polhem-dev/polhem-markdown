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

    /// <summary>Takes the offset in the textarea's text of the line now at the top of the editor.</summary>
    [JSInvokable]
    public Task FirstVisibleChanged(int offset) => editor.OnFirstVisibleChangedAsync(offset);

    /// <summary>Works out the edit of a command; see <see cref="MarkdownEditor.Command"/>.</summary>
    /// <remarks>The edit is an array rather than an object, so trimming cannot remove what serializes it.</remarks>
    [JSInvokable]
    public object?[]? Command(string command, int version, int start, int length, string? argument) =>
        editor.Command(command, version, start, length, argument);
}
