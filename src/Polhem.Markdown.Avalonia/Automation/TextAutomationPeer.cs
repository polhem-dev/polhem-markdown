using Avalonia.Automation;
using Avalonia.Automation.Peers;

namespace Polhem.Markdown.Avalonia.Automation;

/// <summary>
/// A paragraph, heading, table cell or code as an assistive technology sees it: its whole text, with an image as its
/// alternative text and a formula as its LaTeX. Its links are its children, through the controls laid over them.
/// </summary>
internal sealed class TextAutomationPeer(MarkdownTextBlock owner) : TextBlockAutomationPeer(owner)
{
    protected override string? GetNameCore() =>
        AutomationProperties.GetName(owner) is { Length: > 0 } name ? name : owner.AccessibleText;

    protected override bool IsKeyboardFocusableCore() => owner.Focusable;
}
