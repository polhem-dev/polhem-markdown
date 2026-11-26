using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Polhem.Markdown.Avalonia.Automation;

/// <summary>
/// A control an assistive technology sees as one element of the given type, named by its
/// <see cref="global::Avalonia.Automation.AutomationProperties.NameProperty"/>, whatever it is drawn with: an image, or
/// a formula. With <paramref name="invoke"/>, it can be invoked, as an image that opens at full size. An element with no name is left out, as a decorative image is in a browser: it is no control element, and on
/// macOS, where every element is passed on, it has no type a screen reader announces.
/// </summary>
internal sealed class LeafAutomationPeer(Control owner, AutomationControlType type, Action? invoke = null)
    : ControlAutomationPeer(owner), IInvokeProvider
{
    public void Invoke()
    {
        EnsureEnabled();
        invoke?.Invoke();
    }

    protected override object? GetProviderCore(Type providerType) =>
        providerType == typeof(IInvokeProvider) && invoke is null ? null : base.GetProviderCore(providerType);

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        string.IsNullOrEmpty(GetName()) ? AutomationControlType.None : type;

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;

    protected override bool IsControlElementCore() => !string.IsNullOrEmpty(GetName());

    protected override bool IsContentElementCore() => IsControlElementCore();
}
