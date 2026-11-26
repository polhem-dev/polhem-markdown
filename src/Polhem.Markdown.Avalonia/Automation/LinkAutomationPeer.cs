using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace Polhem.Markdown.Avalonia.Automation;

/// <summary>A link of a text as an assistive technology sees it: a hyperlink named by its text, which can be invoked.</summary>
internal sealed class LinkAutomationPeer(LinkFocusTarget owner) : ControlAutomationPeer(owner), IInvokeProvider
{
    public void Invoke()
    {
        EnsureEnabled();
        owner.Activate();
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;

    protected override string GetClassNameCore() => "Hyperlink";

    protected override string? GetNameCore() => base.GetNameCore() is { Length: > 0 } name ? name : owner.LinkName;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

    protected override bool IsContentElementCore() => true;

    protected override bool IsControlElementCore() => true;
}
