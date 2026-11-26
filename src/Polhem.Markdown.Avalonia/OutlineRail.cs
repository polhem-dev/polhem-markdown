using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// The narrow rail along the edge of a <see cref="MarkdownReader"/>: a short line for each heading, shorter and indented
/// for a deeper level, with the line of the section being read in the link color. Pointing at it, clicking it, or Enter
/// or Space on it opens the table of contents.
/// </summary>
internal sealed class OutlineRail : Control
{
    private readonly MarkdownReader _reader;
    private IReadOnlyList<MarkdownHeading> _headings = [];
    private MarkdownHeading? _current;

    public OutlineRail(MarkdownReader reader)
    {
        _reader = reader;
        Width = Size;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Button);
        AutomationProperties.SetIsControlElementOverride(this, true);
    }

    /// <summary>How wide the rail is.</summary>
    public const double Size = 40;

    private const double Top = 20;
    private const double Spacing = 12;
    private const double LineThickness = 2;

    /// <summary>Occurs when a user clicks the rail, or presses Enter or Space on it.</summary>
    public event EventHandler? Activated;

    public void Update(IReadOnlyList<MarkdownHeading> headings, MarkdownHeading? current)
    {
        _headings = headings;
        _current = current;
        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        // A transparent background takes the pointer over the whole rail, not only on its lines.
        context.FillRectangle(Brushes.Transparent, new Rect(Bounds.Size));
        if (_headings.Count == 0)
        {
            return;
        }

        // The lines close up when there are more headings than the rail has room for.
        var spacing = Math.Min(Spacing, Math.Max(LineThickness + 1, (Bounds.Height - 2 * Top) / _headings.Count));
        var viewer = _reader.Viewer;
        var normal = new Pen(viewer?.RuleBrush ?? Brushes.Gray, LineThickness, lineCap: PenLineCap.Round);
        var current = new Pen(viewer?.LinkForeground ?? Brushes.Blue, LineThickness, lineCap: PenLineCap.Round);
        for (var index = 0; index < _headings.Count; index++)
        {
            var heading = _headings[index];
            var (start, width) = Math.Clamp(heading.Level, 1, 6) switch
            {
                1 => (11.0, 18.0),
                2 => (13.0, 14.0),
                _ => (15.0, 10.0),
            };
            var y = Top + index * spacing;
            context.DrawLine(heading == _current ? current : normal, new Point(start, y), new Point(start + width, y));
        }
        if (IsFocused)
        {
            context.DrawRectangle(null, new Pen(viewer?.LinkForeground ?? Brushes.Blue, 1.5), new Rect(Bounds.Size).Deflate(2), 4, 4);
        }
    }

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

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (e.InitialPressMouseButton == MouseButton.Left)
        {
            Activated?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if ((e.Key is Key.Enter or Key.Space) && e.KeyModifiers == KeyModifiers.None)
        {
            Activated?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new RailAutomationPeer(this);

    /// <summary>The rail as a button that an assistive technology presses to open the table of contents.</summary>
    private sealed class RailAutomationPeer(OutlineRail owner) : ControlAutomationPeer(owner), IInvokeProvider
    {
        public void Invoke()
        {
            EnsureEnabled();
            owner.Activated?.Invoke(owner, EventArgs.Empty);
        }

        protected override IReadOnlyList<AutomationPeer>? GetChildrenCore() => null;
    }
}
