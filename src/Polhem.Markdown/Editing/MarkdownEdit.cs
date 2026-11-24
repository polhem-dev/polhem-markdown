namespace Polhem.Markdown.Editing;

/// <summary>
/// One change to a Markdown text: <see cref="Length"/> characters from <see cref="Start"/> replaced by
/// <see cref="Text"/>, and the selection to show afterwards, as offsets in the changed text.
/// </summary>
internal readonly record struct MarkdownEdit(int Start, int Length, string Text, int SelectionStart, int SelectionLength);
