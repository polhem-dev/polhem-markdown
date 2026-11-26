using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Markdown.Model;
using Path = Avalonia.Controls.Shapes.Path;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// The table of contents of a <see cref="MarkdownReader"/>: its label, a button that pins or unpins it, and an entry for
/// each heading. It is as tall as the reader and flush with its rail, floating over the text or pinned beside it, with
/// a line on the side of the text.
/// </summary>
internal sealed class OutlinePanel : Border
{
    private static readonly Geometry s_pinIcon = Geometry.Parse(OutlineGlyphs.PinPath);

    private readonly MarkdownReader _reader;
    private readonly TextBlock _title;
    private readonly ToggleButton _pin;
    private readonly Path _pinIconPath;
    private readonly StackPanel _entries = new() { Spacing = 1 };

    public OutlinePanel(MarkdownReader reader)
    {
        _reader = reader;
        Width = PanelWidth;
        Padding = new Thickness(8);

        _title = new TextBlock { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        // NOTE: the path keeps the 24 by 24 grid of its data and a box scales it, so that the pin turns about the centre
        // of that grid and is the same size upright or leaning, as in the Blazor reader's 24 by 24 view box.
        _pinIconPath = new Path
        {
            Data = s_pinIcon,
            Width = 24,
            Height = 24,
            Stretch = Stretch.None,
            StrokeThickness = OutlineGlyphs.PinStrokeWidth,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        _pin = new ToggleButton
        {
            Content = new Viewbox { Width = 16, Height = 16, Child = _pinIconPath },
            Padding = new Thickness(6),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _pin.Click += (_, _) => reader.SetCurrentValue(MarkdownReader.OutlinePinnedProperty, _pin.IsChecked == true);

        var header = new DockPanel { Margin = new Thickness(8, 4, 0, 8) };
        DockPanel.SetDock(_pin, Dock.Right);
        header.Children.Add(_pin);
        header.Children.Add(_title);

        var list = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _entries,
        };
        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(list);
        Child = layout;

        AutomationProperties.SetLandmarkType(this, AutomationLandmarkType.Navigation);
        AutomationProperties.SetControlTypeOverride(this, AutomationControlType.Group);
        AutomationProperties.SetIsControlElementOverride(this, true);

        // NOTE: bringing an entry into view scrolls the list only. Left to bubble, the request would scroll the text.
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
    }

    /// <summary>How wide the panel is.</summary>
    public const double PanelWidth = 260;

    public IEnumerable<OutlineEntry> Entries => _entries.Children.OfType<OutlineEntry>();

    /// <summary>The button that pins or unpins the panel, for tests.</summary>
    internal ToggleButton PinButton => _pin;

    /// <summary>The pin's icon, for tests.</summary>
    internal Path PinIcon => _pinIconPath;

    /// <summary>Occurs when a user picks a heading.</summary>
    public event EventHandler<MarkdownHeading>? HeadingPicked;

    /// <summary>Occurs when a user presses Escape in the panel.</summary>
    public event EventHandler? Dismissed;

    /// <summary>
    /// Shows the panel's state: pinned or floating, on which side, with which labels, in the viewer's brushes and an
    /// opaque background for its theme.
    /// </summary>
    public void UpdateLook(bool pinned, OutlineSide side, MarkdownViewer? viewer, bool dark)
    {
        _pin.IsChecked = pinned;
        _title.Text = _reader.OutlineLabel;
        AutomationProperties.SetName(this, _reader.OutlineLabel);
        var pin = pinned ? _reader.UnpinOutlineLabel : _reader.PinOutlineLabel;
        AutomationProperties.SetName(_pin, pin);
        ToolTip.SetTip(_pin, pin);

        Background = new SolidColorBrush(dark ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Color.FromRgb(0xFD, 0xFD, 0xFD));
        BorderBrush = viewer?.RuleBrush;
        BorderThickness = side == OutlineSide.Left ? new Thickness(0, 0, 1, 0) : new Thickness(1, 0, 0, 0);
        BoxShadow = pinned
            ? default
            : new BoxShadows(new BoxShadow { OffsetX = side == OutlineSide.Left ? 4 : -4, Blur = 16, Color = Color.FromArgb(0x30, 0, 0, 0) });
        var foreground = viewer?.GetValue(global::Avalonia.Controls.Documents.TextElement.ForegroundProperty) ?? Brushes.Gray;
        _pinIconPath.Stroke = foreground;
        _pinIconPath.Fill = pinned ? foreground : null;
        _pinIconPath.RenderTransform = pinned ? null : new RotateTransform(OutlineGlyphs.UnpinnedAngle);
    }

    /// <summary>Lists <paramref name="headings"/>, marking <paramref name="current"/>.</summary>
    public void Update(MarkdownViewer viewer, IReadOnlyList<MarkdownHeading> headings, MarkdownHeading? current)
    {
        _entries.Children.Clear();
        foreach (var heading in headings)
        {
            var entry = new OutlineEntry(viewer, heading) { IsCurrent = heading == current };
            entry.Click += (_, _) => HeadingPicked?.Invoke(this, heading);
            _entries.Children.Add(entry);
        }
    }

    public void MarkCurrent(MarkdownHeading? current)
    {
        foreach (var entry in Entries)
        {
            var isCurrent = entry.Heading == current;
            if (entry.IsCurrent != isCurrent)
            {
                entry.IsCurrent = isCurrent;
                if (isCurrent && IsEffectivelyVisible)
                {
                    entry.BringIntoView();
                }
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            Dismissed?.Invoke(this, EventArgs.Empty);
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
