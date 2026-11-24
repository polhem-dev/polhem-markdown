using Avalonia;
using Avalonia.Input;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Avalonia;

// Formats, their shortcuts, and the list keys: Enter continues a list, Tab and Shift+Tab move an item in and out.
public partial class MarkdownEditor
{
    /// <summary>
    /// Applies <paramref name="format"/> to the selection, or removes it when the selection already has it, as one step
    /// to undo. Nothing happens when the editor is read-only.
    /// </summary>
    /// <remarks>
    /// An inline format such as <see cref="MarkdownFormat.Bold"/> wraps the selection in its markers, or, with nothing
    /// selected, inserts a pair with the caret between. A line format such as <see cref="MarkdownFormat.BulletList"/>
    /// applies to every line the selection touches. The command shortcut with B, I, K and E applies
    /// <see cref="MarkdownFormat.Bold"/>, <see cref="MarkdownFormat.Italic"/>, <see cref="MarkdownFormat.Link"/> and
    /// <see cref="MarkdownFormat.InlineCode"/>: Ctrl, or Cmd on macOS.
    /// </remarks>
    public void ApplyFormat(MarkdownFormat format)
    {
        if (!IsReadOnly)
        {
            Apply(MarkdownFormatter.Apply(_editor.Text, _editor.SelectionStart, _editor.SelectionLength, format));
        }
    }

    private static MarkdownFormat? Shortcut(Key key) => key switch
    {
        Key.B => MarkdownFormat.Bold,
        Key.I => MarkdownFormat.Italic,
        Key.K => MarkdownFormat.Link,
        Key.E => MarkdownFormat.InlineCode,
        _ => null,
    };

    /// <summary>Handles the keys before the editor inside does, so a list item continues instead of a plain new line.</summary>
    private void OnTextAreaKeyDown(object? sender, KeyEventArgs e)
    {
        if (IsReadOnly)
        {
            return;
        }
        var edit = e switch
        {
            _ when e.KeyModifiers == CommandModifiers() && Shortcut(e.Key) is { } format =>
                MarkdownFormatter.Apply(_editor.Text, _editor.SelectionStart, _editor.SelectionLength, format),
            { Key: Key.Enter, KeyModifiers: KeyModifiers.None } when _editor.SelectionLength == 0 =>
                MarkdownFormatter.ContinueList(_editor.Text, _editor.CaretOffset),
            { Key: Key.Tab, KeyModifiers: KeyModifiers.None or KeyModifiers.Shift } =>
                MarkdownFormatter.IndentListItem(_editor.Text, _editor.SelectionStart, _editor.SelectionLength, e.KeyModifiers == KeyModifiers.Shift),
            _ => null,
        };
        if (edit is { } change)
        {
            Apply(change);
            e.Handled = true;
        }
    }

    /// <summary>The platform's command modifier: Cmd on macOS, Ctrl elsewhere.</summary>
    private static KeyModifiers CommandModifiers() =>
        Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;

    /// <summary>Makes <paramref name="edit"/> as one change, so one undo takes it back, and shows its selection.</summary>
    private void Apply(MarkdownEdit edit)
    {
        _editor.Document.Replace(edit.Start, edit.Length, edit.Text);
        _editor.Select(edit.SelectionStart, edit.SelectionLength);
        _editor.TextArea.Caret.BringCaretToView();
    }
}
