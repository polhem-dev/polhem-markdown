using Avalonia.Automation.Peers;
using Avalonia.Controls;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Text that is drawn but not read, because something else already says it: a bullet, or the title of an alert whose
/// group is named by it.
/// </summary>
/// <remarks>
/// NOTE: <see cref="global::Avalonia.Automation.AutomationProperties.AccessibilityViewProperty"/> and
/// <see cref="global::Avalonia.Automation.AutomationProperties.IsControlElementOverrideProperty"/> do not hide an element
/// on macOS, where Avalonia passes every child on. A peer that is no text has no value, and VoiceOver passes it over.
/// </remarks>
internal sealed class DecorativeTextBlock : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    protected override AutomationPeer OnCreateAutomationPeer() => new NoneAutomationPeer(this);
}
