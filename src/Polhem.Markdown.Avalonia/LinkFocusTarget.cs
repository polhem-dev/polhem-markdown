using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Polhem.Markdown.Avalonia.Automation;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// An invisible control laid over a link of a <see cref="MarkdownTextBlock"/>, which the keyboard and assistive
/// technologies reach the link through: Tab stops on it, Enter follows the link, and it is the hyperlink a screen reader
/// is told has the focus. It does not take the pointer, so a click or a drag still goes to the text.
/// </summary>
/// <remarks>
/// NOTE: a link is a run of the text, which wraps with it, so it cannot be a control. Avalonia reports the focused
/// control to the platform, and offers no way for a text to report a part of itself instead, so the part gets a control.
/// </remarks>
internal sealed class LinkFocusTarget : Control
{
    private readonly MarkdownTextBlock _text;

    public LinkFocusTarget(MarkdownTextBlock text, int index)
    {
        _text = text;
        Index = index;
        Focusable = true;
        IsHitTestVisible = false;
        ClipToBounds = false;

        // The focus is drawn around the link's lines, which a rectangle around the whole target is not when it wraps.
        FocusAdorner = null;
    }

    /// <summary>The index of the link among the text's links.</summary>
    public int Index { get; }

    public MarkdownTextBlock Text => _text;

    /// <summary>What an assistive technology reads for the link.</summary>
    public string LinkName => _text.LinkName(Index);

    public void Activate() => _text.ActivateLink(Index);

    protected override AutomationPeer OnCreateAutomationPeer() => new LinkAutomationPeer(this);

    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);
        InvalidateVisual();
    }

    protected override void OnLostFocus(FocusChangedEventArgs e)
    {
        base.OnLostFocus(e);
        InvalidateVisual();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
        {
            Activate();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    public override void Render(DrawingContext context)
    {
        if (!IsFocused)
        {
            return;
        }
        var pen = new Pen(_text.FocusBrush, 1.5);
        var offset = new Vector(-Bounds.X, -Bounds.Y);
        foreach (var rectangle in _text.LinkRectangles(Index))
        {
            context.DrawRectangle(null, pen, rectangle.Translate(offset).Inflate(FocusMargin), 2, 2);
        }
    }

    /// <summary>How far the focus rectangle lies outside the link's text.</summary>
    private const double FocusMargin = 1.5;
}
