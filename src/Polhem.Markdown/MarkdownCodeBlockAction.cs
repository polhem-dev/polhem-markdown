namespace Polhem.Markdown;

/// <summary>
/// A button a host adds to the code blocks of a viewer, next to the copy button, such as "Insert at cursor" or "Run".
/// The Avalonia and Blazor viewers take the same actions and show them the same way.
/// </summary>
/// <remarks>
/// A button is always shown while <see cref="IsVisible"/> allows it, as the copy button is, so it works with no pointer
/// that hovers, such as a finger. It is reached with Tab, and its accessible name and tooltip are <see cref="Label"/>.
/// </remarks>
public sealed class MarkdownCodeBlockAction
{
    private readonly Func<MarkdownCodeBlock, Task> _invoked;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarkdownCodeBlockAction"/> class with work that completes before it
    /// returns.
    /// </summary>
    /// <param name="label">The button's text, or with an <see cref="IconPath"/> its accessible name and tooltip.</param>
    /// <param name="invoked">What the button does, given the block it belongs to.</param>
    public MarkdownCodeBlockAction(string label, Action<MarkdownCodeBlock> invoked)
    {
        ArgumentNullException.ThrowIfNull(invoked);
        Label = ValidLabel(label);
        _invoked = block =>
        {
            invoked(block);
            return Task.CompletedTask;
        };
    }

    /// <summary>Initializes a new instance of the <see cref="MarkdownCodeBlockAction"/> class with work that is awaited.</summary>
    /// <param name="label">The button's text, or with an <see cref="IconPath"/> its accessible name and tooltip.</param>
    /// <param name="invoked">What the button does, given the block it belongs to.</param>
    public MarkdownCodeBlockAction(string label, Func<MarkdownCodeBlock, Task> invoked)
    {
        ArgumentNullException.ThrowIfNull(invoked);
        Label = ValidLabel(label);
        _invoked = invoked;
    }

    /// <summary>
    /// Gets the button's text. With an <see cref="IconPath"/> the button shows the icon instead, and the label is its
    /// accessible name and tooltip.
    /// </summary>
    public string Label { get; }

    /// <summary>
    /// Gets the button's icon as SVG path data drawn in a 24 by 24 box and filled with the text color, as the copy button's
    /// icon is, or <see langword="null"/> to show <see cref="Label"/> as text.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>
    /// Gets which blocks show the button, such as only those whose <see cref="MarkdownCodeBlock.Language"/> is
    /// <c>markdown</c>, or <see langword="null"/> to show it on every code block.
    /// </summary>
    /// <remarks>
    /// The viewer asks when it draws a block. When the answer depends on something else, such as whether an editor has the
    /// focus, the host asks the viewer to ask again when that changes: <c>RefreshCodeBlockActions</c> in the Avalonia
    /// viewer and <c>RefreshCodeBlockActionsAsync</c> in the Blazor viewer.
    /// </remarks>
    public Func<MarkdownCodeBlock, bool>? IsVisible { get; init; }

    /// <summary>Whether the button is shown on <paramref name="block"/>.</summary>
    internal bool IsShownOn(MarkdownCodeBlock block) => IsVisible?.Invoke(block) ?? true;

    /// <summary>Does what the button does for <paramref name="block"/>.</summary>
    internal Task InvokeAsync(MarkdownCodeBlock block) => _invoked(block);

    private static string ValidLabel(string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return label;
    }
}
