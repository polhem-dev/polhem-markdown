using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// The table of contents of a <see cref="MarkdownViewer"/>: its label, a button that pins or unpins it, and an entry
/// for each heading. The viewer shows it floating over the text or pinned beside it.
/// </summary>
internal sealed class OutlinePanel : Border
{
    /// <summary>A pin, upright: the panel floats and can be pinned.</summary>
    internal static readonly Geometry PinIcon = Geometry.Parse(
        "M9 2h6l-1 2v5l3 3v2h-4.25v6L12 22l-.75-2v-6H7v-2l3-3V4z");

    private readonly MarkdownViewer _viewer;
    private readonly TextBlock _title;
    private readonly ToggleButton _pin;
    private readonly StackPanel _entries = new() { Spacing = 1 };

    public OutlinePanel(MarkdownViewer viewer)
    {
        _viewer = viewer;
        Padding = new Thickness(8);
        BorderThickness = new Thickness(1);
        CornerRadius = new CornerRadius(6);
        Bind(BorderBrushProperty, viewer.GetObservable(MarkdownViewer.RuleBrushProperty));

        _title = new TextBlock { FontWeight = FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center };
        _pin = new ToggleButton
        {
            Content = new Path { Data = PinIcon, Width = 14, Height = 14, Stretch = Stretch.Uniform },
            Padding = new Thickness(6),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        ((Path)_pin.Content).Bind(Path.FillProperty, viewer.GetObservable(TextElement.ForegroundProperty));
        _pin.Click += (_, _) => viewer.SetCurrentValue(MarkdownViewer.OutlinePinnedProperty, _pin.IsChecked == true);

        var header = new DockPanel { Margin = new Thickness(8, 0, 0, 6) };
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
        UpdateLabels();
        UpdateBackground();

        // NOTE: bringing an entry into view scrolls the list only. Left to bubble, the request would scroll the text
        // around the viewer to wherever the panel was last placed.
        AddHandler(RequestBringIntoViewEvent, (_, e) => e.Handled = true);
    }

    /// <summary>Whether the panel is pinned beside the text, as opposed to floating over it.</summary>
    public bool IsPinned
    {
        get => _pin.IsChecked == true;
        set
        {
            _pin.IsChecked = value;
            BoxShadow = value ? default : new BoxShadows(new BoxShadow { OffsetY = 4, Blur = 16, Color = Color.FromArgb(0x40, 0, 0, 0) });
            UpdateLabels();
        }
    }

    public IEnumerable<OutlineEntry> Entries => _entries.Children.OfType<OutlineEntry>();

    /// <summary>Occurs when a user picks a heading.</summary>
    public event EventHandler<MarkdownHeading>? HeadingPicked;

    /// <summary>Occurs when a user presses Escape in the panel.</summary>
    public event EventHandler? Dismissed;

    public void UpdateLabels()
    {
        _title.Text = _viewer.OutlineLabel;
        AutomationProperties.SetName(this, _viewer.OutlineLabel);
        var pin = IsPinned ? _viewer.UnpinOutlineLabel : _viewer.PinOutlineLabel;
        AutomationProperties.SetName(_pin, pin);
        ToolTip.SetTip(_pin, pin);
    }

    /// <summary>
    /// Gives the panel an opaque background for the viewer's theme, so the text under a floating panel does not show
    /// through.
    /// </summary>
    public void UpdateBackground() =>
        Background = new SolidColorBrush(_viewer.IsDarkTheme ? Color.FromRgb(0x1E, 0x1E, 0x1E) : Colors.White);

    /// <summary>Lists <paramref name="headings"/>, keeping the mark on the current one where it still is.</summary>
    public void Update(IReadOnlyList<MarkdownHeading> headings, MarkdownHeading? current)
    {
        _entries.Children.Clear();
        foreach (var heading in headings)
        {
            var entry = new OutlineEntry(_viewer, heading) { IsCurrent = heading == current };
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
