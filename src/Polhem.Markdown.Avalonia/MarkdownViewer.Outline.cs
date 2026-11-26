using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Path = Avalonia.Controls.Shapes.Path;

namespace Polhem.Markdown.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>Defines the <see cref="ShowOutline"/> property.</summary>
    public static readonly StyledProperty<bool> ShowOutlineProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(ShowOutline));

    /// <summary>Defines the <see cref="OutlinePinned"/> property, which binds both ways by default.</summary>
    public static readonly StyledProperty<bool> OutlinePinnedProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(OutlinePinned), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="OutlineSide"/> property.</summary>
    public static readonly StyledProperty<OutlineSide> OutlineSideProperty =
        AvaloniaProperty.Register<MarkdownViewer, OutlineSide>(nameof(OutlineSide));

    /// <summary>Defines the <see cref="OutlineLabel"/> property.</summary>
    public static readonly StyledProperty<string> OutlineLabelProperty =
        AvaloniaProperty.Register<MarkdownViewer, string>(nameof(OutlineLabel), "Contents");

    /// <summary>Defines the <see cref="PinOutlineLabel"/> property.</summary>
    public static readonly StyledProperty<string> PinOutlineLabelProperty =
        AvaloniaProperty.Register<MarkdownViewer, string>(nameof(PinOutlineLabel), "Pin");

    /// <summary>Defines the <see cref="UnpinOutlineLabel"/> property.</summary>
    public static readonly StyledProperty<string> UnpinOutlineLabelProperty =
        AvaloniaProperty.Register<MarkdownViewer, string>(nameof(UnpinOutlineLabel), "Unpin");

    private const double OutlineColumnWidth = 220;
    private const double FloatingOutlineWidth = 260;
    private const double OutlineGap = 16;
    private const double OutlineButtonSize = 32;

    /// <summary>
    /// How far below the top of the view a heading still counts as the section being read, as the text moves a little
    /// when it reflows. The Blazor viewer's script uses the same distance.
    /// </summary>
    private const double ReadingMargin = 24;

    private static readonly Geometry s_outlineIcon = Geometry.Parse("M3 5h2v2H3zm4 0h14v2H7zM3 11h2v2H3zm4 0h14v2H7zm-4 6h2v2H3zm4 0h14v2H7z");

    private OutlinePanel? _outlinePanel;
    private ToggleButton? _outlineButton;
    private ScrollViewer? _outlineScroller;
    private MarkdownHeading? _currentHeading;

    /// <summary>
    /// Gets or sets whether the viewer offers a table of contents of the text's headings: a button in its top corner that
    /// opens the list floating over the text, which <see cref="OutlinePinned"/> pins beside it. Off by default. The
    /// button and the list stay in view as the text scrolls in the nearest <see cref="ScrollViewer"/>, and the section
    /// being read is marked.
    /// </summary>
    public bool ShowOutline
    {
        get => GetValue(ShowOutlineProperty);
        set => SetValue(ShowOutlineProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the table of contents is pinned as a column beside the text, rather than opened from a
    /// button. The pin button in the list changes it, and it binds both ways, so a host can remember it.
    /// </summary>
    public bool OutlinePinned
    {
        get => GetValue(OutlinePinnedProperty);
        set => SetValue(OutlinePinnedProperty, value);
    }

    /// <summary>Gets or sets the side the table of contents is on. The left by default.</summary>
    public OutlineSide OutlineSide
    {
        get => GetValue(OutlineSideProperty);
        set => SetValue(OutlineSideProperty, value);
    }

    /// <summary>Gets or sets the title of the table of contents, which also names its button. Set it to translate it.</summary>
    public string OutlineLabel
    {
        get => GetValue(OutlineLabelProperty);
        set => SetValue(OutlineLabelProperty, value);
    }

    /// <summary>Gets or sets the accessible name and tooltip of the button that pins the table of contents.</summary>
    public string PinOutlineLabel
    {
        get => GetValue(PinOutlineLabelProperty);
        set => SetValue(PinOutlineLabelProperty, value);
    }

    /// <summary>Gets or sets the accessible name and tooltip of the button that unpins the table of contents.</summary>
    public string UnpinOutlineLabel
    {
        get => GetValue(UnpinOutlineLabelProperty);
        set => SetValue(UnpinOutlineLabelProperty, value);
    }

    /// <summary>The table of contents, once <see cref="ShowOutline"/> has been on, for tests.</summary>
    internal OutlinePanel? Outline => _outlinePanel;

    /// <summary>The button that opens the floating table of contents, for tests.</summary>
    internal ToggleButton? OutlineButton => _outlineButton;

    /// <summary>The heading of the section being read, for tests.</summary>
    internal MarkdownHeading? CurrentHeading => _currentHeading;

    /// <summary>Whether the table of contents is shown: it is on, and the text has headings.</summary>
    private bool OutlineShown => ShowOutline && Headings.Count > 0;

    /// <summary>Whether the floating table of contents is open.</summary>
    internal bool OutlineOpen => _outlineButton?.IsChecked == true && !OutlinePinned && OutlineShown;

    /// <summary>The room the table of contents takes beside the text: its column, or its button.</summary>
    private double OutlineInset => !OutlineShown ? 0 : OutlinePinned ? OutlineColumnWidth + OutlineGap : OutlineButtonSize + 8;

    private void OnOutlinePropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == ShowOutlineProperty && ShowOutline)
        {
            EnsureOutline();
        }
        if (_outlinePanel is null)
        {
            return;
        }
        if (change.Property == OutlineLabelProperty || change.Property == PinOutlineLabelProperty
            || change.Property == UnpinOutlineLabelProperty)
        {
            _outlinePanel.UpdateLabels();
            AutomationProperties.SetName(_outlineButton!, OutlineLabel);
            ToolTip.SetTip(_outlineButton!, OutlineLabel);
        }
        else if (change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            _outlinePanel.UpdateBackground();
        }
        else if (change.Property == OutlinePinnedProperty)
        {
            _outlinePanel.IsPinned = OutlinePinned;
            _outlineButton!.IsChecked = false;
        }
        UpdateOutlineVisibility();
        InvalidateMeasure();
    }

    private void EnsureOutline()
    {
        if (_outlinePanel is not null)
        {
            return;
        }
        _outlinePanel = new OutlinePanel(this) { IsPinned = OutlinePinned };
        _outlinePanel.Update(Headings, _currentHeading);
        _outlinePanel.HeadingPicked += (_, heading) => PickHeading(heading);
        _outlinePanel.Dismissed += (_, _) => CloseFloatingOutline(focusButton: true);

        var icon = new Path { Data = s_outlineIcon, Width = 16, Height = 16, Stretch = Stretch.Uniform };
        icon.Bind(Path.FillProperty, this.GetObservable(TextElement.ForegroundProperty));
        _outlineButton = new ToggleButton
        {
            Content = icon,
            Width = OutlineButtonSize,
            Height = OutlineButtonSize,
            Padding = new Thickness(0),
            HorizontalContentAlignment = global::Avalonia.Layout.HorizontalAlignment.Center,
            VerticalContentAlignment = global::Avalonia.Layout.VerticalAlignment.Center,
        };
        AutomationProperties.SetName(_outlineButton, OutlineLabel);
        ToolTip.SetTip(_outlineButton, OutlineLabel);
        _outlineButton.IsCheckedChanged += (_, _) =>
        {
            UpdateOutlineVisibility();
            InvalidateArrange();
            if (OutlineOpen)
            {
                // The panel takes the focus once it is laid out, so the keyboard goes on from the section being read.
                Dispatcher.UIThread.Post(() =>
                    (_outlinePanel.Entries.FirstOrDefault(entry => entry.IsCurrent) ?? _outlinePanel.Entries.FirstOrDefault())?.Focus(NavigationMethod.Tab));
            }
        };

        // NOTE: the table of contents comes before the text, so Tab and a screen reader reach it first, as the Blazor
        // viewer's navigation comes before its text; drawn on top of the text, it floats over it.
        _outlineButton.ZIndex = 1;
        _outlinePanel.ZIndex = 1;
        LogicalChildren.Insert(0, _outlinePanel);
        LogicalChildren.Insert(0, _outlineButton);
        VisualChildren.Insert(0, _outlinePanel);
        VisualChildren.Insert(0, _outlineButton);
        UpdateOutlineVisibility();
        WatchPointerForOutline(TopLevel.GetTopLevel(this));
    }

    /// <summary>
    /// Watches presses anywhere in the window, so one beside the floating table of contents closes it even where the
    /// viewer draws nothing, such as between blocks or outside the viewer.
    /// </summary>
    private void WatchPointerForOutline(TopLevel? topLevel)
    {
        topLevel?.RemoveHandler(PointerPressedEvent, OnPointerPressedForOutline);
        topLevel?.AddHandler(PointerPressedEvent, OnPointerPressedForOutline, RoutingStrategies.Tunnel);
    }

    private void UpdateOutlineVisibility()
    {
        if (_outlinePanel is null)
        {
            return;
        }
        _outlineButton!.IsVisible = OutlineShown && !OutlinePinned;
        _outlinePanel.IsVisible = OutlineShown && (OutlinePinned || OutlineOpen);
    }

    /// <summary>Lists the new headings in the table of contents.</summary>
    private void UpdateOutlineHeadings()
    {
        if (_outlinePanel is null)
        {
            return;
        }
        if (_currentHeading is { } current && !Headings.Contains(current))
        {
            _currentHeading = null;
        }
        _outlinePanel.Update(Headings, _currentHeading);
        UpdateOutlineVisibility();
        InvalidateMeasure();
        Dispatcher.UIThread.Post(UpdateCurrentHeading, DispatcherPriority.Background);
    }

    private void PickHeading(MarkdownHeading heading)
    {
        CloseFloatingOutline(focusButton: false);
        ScrollToAnchor(heading.Id, moveFocus: true);
        _currentHeading = heading;
        _outlinePanel?.MarkCurrent(heading);
    }

    private void CloseFloatingOutline(bool focusButton)
    {
        if (!OutlineOpen)
        {
            return;
        }
        _outlineButton!.IsChecked = false;
        if (focusButton)
        {
            _outlineButton.Focus(NavigationMethod.Tab);
        }
    }

    /// <summary>A press outside the floating table of contents closes it, as a click beside a menu does.</summary>
    private void OnPointerPressedForOutline(object? sender, PointerPressedEventArgs e)
    {
        if (OutlineOpen && e.Source is Visual source
            && !_outlinePanel!.IsVisualAncestorOf(source) && source != _outlinePanel
            && !_outlineButton!.IsVisualAncestorOf(source) && source != _outlineButton)
        {
            CloseFloatingOutline(focusButton: false);
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _outlineScroller = this.FindAncestorOfType<ScrollViewer>();
        if (_outlineScroller is not null)
        {
            _outlineScroller.ScrollChanged += OnScrollChangedForOutline;
        }
        if (_outlinePanel is not null)
        {
            WatchPointerForOutline(TopLevel.GetTopLevel(this));
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        (e.RootVisual as TopLevel)?.RemoveHandler(PointerPressedEvent, OnPointerPressedForOutline);
        if (_outlineScroller is not null)
        {
            _outlineScroller.ScrollChanged -= OnScrollChangedForOutline;
            _outlineScroller = null;
        }
    }

    private void OnScrollChangedForOutline(object? sender, ScrollChangedEventArgs e)
    {
        if (!OutlineShown)
        {
            return;
        }

        // The table of contents stays in view: it is placed by where the view is.
        InvalidateArrange();
        UpdateCurrentHeading();
    }

    /// <summary>
    /// Marks the heading of the section at the top of the view, counting a heading just below the top as read. At the
    /// end of the text the last headings cannot reach the top, so a heading picked there would give way to the one above
    /// it at once; there the bottom of the view decides.
    /// </summary>
    private void UpdateCurrentHeading()
    {
        if (_outlinePanel is null || _outlineScroller is not { } scroller
            || this.TranslatePoint(default, scroller) is not { } origin)
        {
            return;
        }
        var atEnd = scroller.Offset.Y >= scroller.Extent.Height - scroller.Viewport.Height - 1;
        var position = (atEnd ? scroller.Viewport.Height : ReadingMargin) - origin.Y;
        if (GetSourceOffset(position) is not { } offset)
        {
            return;
        }
        var current = Headings.LastOrDefault(heading => heading.SourceOffset <= offset);
        if (current != _currentHeading)
        {
            _currentHeading = current;
            _outlinePanel.MarkCurrent(current);
        }
    }

    /// <summary>
    /// The band of the viewer in view, from its top: where the table of contents is placed so it stays in view, and how
    /// tall it can be. Without a scroll viewer around it, the whole viewer.
    /// </summary>
    private (double Top, double Height) VisibleBand(double height)
    {
        if (_outlineScroller is { Viewport.Height: > 0 } scroller && this.TranslatePoint(default, scroller) is { } origin)
        {
            var visible = Math.Min(scroller.Viewport.Height, height);
            return (Math.Clamp(-origin.Y, 0, Math.Max(0, height - visible)), visible);
        }
        return (0, double.IsFinite(height) ? height : 0);
    }

    private void MeasureOutline(Size availableSize)
    {
        if (!OutlineShown || _outlinePanel is null)
        {
            return;
        }
        var height = _outlineScroller is { Viewport.Height: > 0 } scroller ? scroller.Viewport.Height : availableSize.Height;
        _outlineButton!.Measure(availableSize);
        _outlinePanel.Measure(new Size(OutlinePinned ? OutlineColumnWidth : FloatingOutlineWidth, height));
    }

    private void ArrangeOutline(Size finalSize)
    {
        if (!OutlineShown || _outlinePanel is null)
        {
            return;
        }
        var left = OutlineSide == OutlineSide.Left;
        var (top, height) = VisibleBand(finalSize.Height);
        if (OutlinePinned)
        {
            _outlinePanel.Arrange(new Rect(left ? 0 : finalSize.Width - OutlineColumnWidth, top, OutlineColumnWidth, height));
            return;
        }
        _outlineButton!.Arrange(new Rect(left ? 0 : finalSize.Width - OutlineButtonSize, top, OutlineButtonSize, OutlineButtonSize));
        if (OutlineOpen)
        {
            var panelTop = top + OutlineButtonSize + 4;
            var panelHeight = Math.Min(_outlinePanel.DesiredSize.Height, Math.Max(0, height - OutlineButtonSize - 4));
            var width = Math.Min(FloatingOutlineWidth, finalSize.Width);
            _outlinePanel.Arrange(new Rect(left ? 0 : finalSize.Width - width, panelTop, width, panelHeight));
        }
    }
}
