using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Metadata;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A reading area for a <see cref="MarkdownViewer"/>: it scrolls the viewer, keeps its text in a centred column, and
/// offers a table of contents of its headings. A narrow rail along its edge draws a short line for each heading,
/// marking the section being read. Pointing at the rail, or clicking it, slides the list of headings out over the text;
/// its pin keeps it beside the text as a column, the text centring in the room that is left.
/// </summary>
/// <remarks>
/// The viewer is the reader's content and keeps its own properties: the text, options, colors and events. The reader
/// scrolls it, so it needs no <see cref="ScrollViewer"/> around it, and gives it its height.
/// </remarks>
public class MarkdownReader : Control
{
    /// <summary>Defines the <see cref="Viewer"/> property.</summary>
    public static readonly StyledProperty<MarkdownViewer?> ViewerProperty =
        AvaloniaProperty.Register<MarkdownReader, MarkdownViewer?>(nameof(Viewer));

    /// <summary>Defines the <see cref="OutlinePinned"/> property, which binds both ways by default.</summary>
    public static readonly StyledProperty<bool> OutlinePinnedProperty =
        AvaloniaProperty.Register<MarkdownReader, bool>(nameof(OutlinePinned), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="OutlineSide"/> property.</summary>
    public static readonly StyledProperty<OutlineSide> OutlineSideProperty =
        AvaloniaProperty.Register<MarkdownReader, OutlineSide>(nameof(OutlineSide));

    /// <summary>Defines the <see cref="TextMaxWidth"/> property.</summary>
    public static readonly StyledProperty<double> TextMaxWidthProperty =
        AvaloniaProperty.Register<MarkdownReader, double>(nameof(TextMaxWidth), 800);

    /// <summary>Defines the <see cref="OutlineLabel"/> property.</summary>
    public static readonly StyledProperty<string> OutlineLabelProperty =
        AvaloniaProperty.Register<MarkdownReader, string>(nameof(OutlineLabel), "Contents");

    /// <summary>Defines the <see cref="PinOutlineLabel"/> property.</summary>
    public static readonly StyledProperty<string> PinOutlineLabelProperty =
        AvaloniaProperty.Register<MarkdownReader, string>(nameof(PinOutlineLabel), "Pin");

    /// <summary>Defines the <see cref="UnpinOutlineLabel"/> property.</summary>
    public static readonly StyledProperty<string> UnpinOutlineLabelProperty =
        AvaloniaProperty.Register<MarkdownReader, string>(nameof(UnpinOutlineLabel), "Unpin");

    /// <summary>
    /// How far below the top of the view a heading still counts as the section being read, as the text moves a little
    /// when it reflows. The Blazor reader's script uses the same distance.
    /// </summary>
    private const double ReadingMargin = 24;

    /// <summary>How long the pointer may be away from the rail and the list before a list it opened closes.</summary>
    private static readonly TimeSpan s_closeDelay = TimeSpan.FromMilliseconds(250);

    private readonly ScrollViewer _scroller;
    private readonly Border _column;
    private readonly OutlineRail _rail;
    private readonly OutlinePanel _panel;
    private MarkdownHeading? _currentHeading;
    private bool _open;
    private bool _openedByPointer;
    private IDisposable? _closeTimer;
    private TopLevel? _topLevel;

    /// <summary>Initializes a new instance of the <see cref="MarkdownReader"/> class.</summary>
    public MarkdownReader()
    {
        _column = new Border
        {
            Padding = new Thickness(16, 8, 16, 24),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            MaxWidth = TextMaxWidth,
        };
        _scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = _column,
        };
        _scroller.ScrollChanged += (_, _) => UpdateCurrentHeading();

        _rail = new OutlineRail(this);
        _rail.Activated += (_, _) => ToggleFromRail();
        _rail.PointerEntered += (_, _) => OpenByPointer();
        _rail.PointerExited += (_, _) => CloseSoon();

        _panel = new OutlinePanel(this) { ZIndex = 1, IsVisible = false };
        _panel.HeadingPicked += (_, heading) => PickHeading(heading);
        _panel.Dismissed += (_, _) => Close(focusRail: true);
        _panel.PointerEntered += (_, _) => _closeTimer?.Dispose();
        _panel.PointerExited += (_, _) => CloseSoon();

        // NOTE: the rail and the list come before the text, so Tab and a screen reader reach them first, as the Blazor
        // reader's navigation comes before its text; drawn on top, the floating list covers the text.
        foreach (var child in new Control[] { _rail, _panel, _scroller })
        {
            LogicalChildren.Add(child);
            VisualChildren.Add(child);
        }
        UpdateLook();
    }

    /// <summary>Gets or sets the viewer the reader shows, which is its content in XAML.</summary>
    [Content]
    public MarkdownViewer? Viewer
    {
        get => GetValue(ViewerProperty);
        set => SetValue(ViewerProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the table of contents is pinned beside the text as a column, rather than sliding out over
    /// it. Its pin changes it, and it binds both ways, so a host can remember it.
    /// </summary>
    public bool OutlinePinned
    {
        get => GetValue(OutlinePinnedProperty);
        set => SetValue(OutlinePinnedProperty, value);
    }

    /// <summary>Gets or sets the side the rail and the table of contents are on. The left by default.</summary>
    public OutlineSide OutlineSide
    {
        get => GetValue(OutlineSideProperty);
        set => SetValue(OutlineSideProperty, value);
    }

    /// <summary>
    /// Gets or sets the widest the column of text grows, centred in the room beside the rail and a pinned table of
    /// contents. 800 by default; <see cref="double.PositiveInfinity"/> lets the text take all the room.
    /// </summary>
    public double TextMaxWidth
    {
        get => GetValue(TextMaxWidthProperty);
        set => SetValue(TextMaxWidthProperty, value);
    }

    /// <summary>Gets or sets the title of the table of contents, which also names the rail. Set it to translate it.</summary>
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

    /// <summary>
    /// Gets the scroll viewer the reader scrolls the text in, for a host that follows or sets where the text is
    /// scrolled, as one that keeps an editor in step with the viewer does.
    /// </summary>
    public ScrollViewer ScrollViewer => _scroller;

    internal ScrollViewer Scroller => _scroller;

    internal OutlineRail Rail => _rail;

    internal OutlinePanel Outline => _panel;

    /// <summary>Whether the table of contents is showing over the text.</summary>
    internal bool OutlineOpen => _open && !OutlinePinned && HasHeadings;

    /// <summary>The heading of the section being read, for tests.</summary>
    internal MarkdownHeading? CurrentHeading => _currentHeading;

    private bool HasHeadings => Viewer is { Headings.Count: > 0 };

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ViewerProperty)
        {
            if (change.OldValue is MarkdownViewer old)
            {
                old.HeadingsChanged -= OnHeadingsChanged;
            }
            _column.Child = Viewer;
            if (Viewer is { } viewer)
            {
                viewer.HeadingsChanged += OnHeadingsChanged;
            }
            OnHeadingsChanged(this, EventArgs.Empty);
        }
        else if (change.Property == TextMaxWidthProperty)
        {
            _column.MaxWidth = TextMaxWidth;
        }
        else if (change.Property == OutlinePinnedProperty)
        {
            _open = false;
            UpdateLook();
            InvalidateMeasure();
        }
        else if (change.Property == OutlineSideProperty || change.Property == OutlineLabelProperty
            || change.Property == PinOutlineLabelProperty || change.Property == UnpinOutlineLabelProperty
            || change.Property == ThemeVariantScope.ActualThemeVariantProperty)
        {
            UpdateLook();
            InvalidateMeasure();
        }
    }

    private void UpdateLook()
    {
        var dark = ActualThemeVariant is { } theme
            && (theme == ThemeVariant.Dark || theme.InheritVariant == ThemeVariant.Dark);
        _panel.UpdateLook(OutlinePinned, OutlineSide, Viewer, dark);
        global::Avalonia.Automation.AutomationProperties.SetName(_rail, OutlineLabel);
        ToolTip.SetTip(_rail, OutlineLabel);
        _rail.IsVisible = HasHeadings;
        _panel.IsVisible = HasHeadings && (OutlinePinned || _open);
    }

    private void OnHeadingsChanged(object? sender, EventArgs e)
    {
        if (_currentHeading is { } current && Viewer?.Headings.Contains(current) != true)
        {
            _currentHeading = null;
        }
        if (Viewer is { } viewer)
        {
            _panel.Update(viewer, viewer.Headings, _currentHeading);
        }
        _rail.Update(Viewer?.Headings ?? [], _currentHeading);
        UpdateLook();
        InvalidateMeasure();
        Dispatcher.UIThread.Post(UpdateCurrentHeading, DispatcherPriority.Background);
    }

    private void OpenByPointer()
    {
        _closeTimer?.Dispose();
        if (OutlinePinned || OutlineOpen)
        {
            return;
        }
        _openedByPointer = true;
        SetOpen(true);
    }

    /// <summary>A click on the rail, or Enter on it, opens the list to stay, or closes it.</summary>
    private void ToggleFromRail()
    {
        if (OutlinePinned)
        {
            return;
        }
        if (OutlineOpen && !_openedByPointer)
        {
            Close(focusRail: false);
            return;
        }
        _openedByPointer = false;
        SetOpen(true);

        // The list takes the focus once it is laid out, so the keyboard goes on from the section being read.
        Dispatcher.UIThread.Post(() =>
            (_panel.Entries.FirstOrDefault(entry => entry.IsCurrent) ?? _panel.Entries.FirstOrDefault())?.Focus(NavigationMethod.Tab));
    }

    /// <summary>Closes a list the pointer opened, once the pointer has been away from the rail and the list a moment.</summary>
    private void CloseSoon()
    {
        if (!_openedByPointer || !OutlineOpen)
        {
            return;
        }
        _closeTimer?.Dispose();
        _closeTimer = DispatcherTimer.RunOnce(() =>
        {
            if (_openedByPointer && !_rail.IsPointerOver && !_panel.IsPointerOver)
            {
                Close(focusRail: false);
            }
        }, s_closeDelay);
    }

    private void Close(bool focusRail)
    {
        if (!OutlineOpen)
        {
            return;
        }
        SetOpen(false);
        if (focusRail)
        {
            _rail.Focus(NavigationMethod.Tab);
        }
    }

    private void SetOpen(bool open)
    {
        _open = open;
        UpdateLook();
        InvalidateArrange();
    }

    private void PickHeading(MarkdownHeading heading)
    {
        Close(focusRail: false);
        Viewer?.ScrollToAnchor(heading.Id, moveFocus: true);
        SetCurrent(heading);
    }

    private void SetCurrent(MarkdownHeading? heading)
    {
        if (heading == _currentHeading)
        {
            return;
        }
        _currentHeading = heading;
        _panel.MarkCurrent(heading);
        _rail.Update(Viewer?.Headings ?? [], heading);
    }

    /// <summary>
    /// Marks the heading of the section at the top of the view, counting a heading just below the top as read. At the
    /// end of the text the last headings cannot reach the top, so a heading picked there would give way to the one above
    /// it at once; there the bottom of the view decides.
    /// </summary>
    private void UpdateCurrentHeading()
    {
        if (Viewer is not { } viewer || viewer.TranslatePoint(default, _scroller) is not { } origin)
        {
            return;
        }
        var atEnd = _scroller.Offset.Y >= _scroller.Extent.Height - _scroller.Viewport.Height - 1;
        var position = (atEnd ? _scroller.Viewport.Height : ReadingMargin) - origin.Y;
        if (viewer.GetSourceOffset(position) is { } offset)
        {
            SetCurrent(viewer.Headings.LastOrDefault(heading => heading.SourceOffset <= offset));
        }
    }

    /// <summary>A press outside the floating list closes it, as a click beside a menu does.</summary>
    private void OnPointerPressedAnywhere(object? sender, PointerPressedEventArgs e)
    {
        if (OutlineOpen && e.Source is Visual source
            && !_panel.IsVisualAncestorOf(source) && source != _panel
            && !_rail.IsVisualAncestorOf(source) && source != _rail)
        {
            Close(focusRail: false);
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _topLevel = TopLevel.GetTopLevel(this);
        _topLevel?.AddHandler(PointerPressedEvent, OnPointerPressedAnywhere, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _topLevel?.RemoveHandler(PointerPressedEvent, OnPointerPressedAnywhere);
        _topLevel = null;
        _closeTimer?.Dispose();
    }

    /// <summary>The room the rail and a pinned list take beside the text.</summary>
    private double Inset => !HasHeadings ? 0 : OutlineRail.Size + (OutlinePinned ? OutlinePanel.PanelWidth : 0);

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset = Inset;
        _scroller.Measure(availableSize.WithWidth(Math.Max(0, availableSize.Width - inset)));
        _rail.Measure(availableSize);
        _panel.Measure(availableSize);
        var height = double.IsFinite(availableSize.Height) ? availableSize.Height : _scroller.DesiredSize.Height;
        return new Size(_scroller.DesiredSize.Width + inset, height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var left = OutlineSide == OutlineSide.Left;
        var inset = Inset;
        var width = finalSize.Width;
        _scroller.Arrange(new Rect(left ? inset : 0, 0, Math.Max(0, width - inset), finalSize.Height));
        if (!HasHeadings)
        {
            return finalSize;
        }
        _rail.Arrange(new Rect(left ? 0 : width - OutlineRail.Size, 0, OutlineRail.Size, finalSize.Height));
        var panelWidth = Math.Min(OutlinePanel.PanelWidth, Math.Max(0, width - OutlineRail.Size));
        _panel.Arrange(new Rect(left ? OutlineRail.Size : width - OutlineRail.Size - panelWidth, 0, panelWidth, finalSize.Height));
        return finalSize;
    }
}
