using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Polhem.Markdown.Avalonia;

// Finding text in the viewer (polhem-dev/polhem-markdown#85).
public partial class MarkdownViewer
{
    /// <summary>Defines the <see cref="FindMatchBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> FindMatchBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(FindMatchBrush), new SolidColorBrush(Color.FromArgb(0x66, 0xFF, 0xD5, 0x00)));

    /// <summary>Defines the <see cref="FindCurrentMatchBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> FindCurrentMatchBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(FindCurrentMatchBrush), new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0x96, 0x32)));

    /// <summary>Defines the <see cref="FindBarLabels"/> property.</summary>
    public static readonly StyledProperty<Func<MarkdownFindBarItem, string?>?> FindBarLabelsProperty =
        AvaloniaProperty.Register<MarkdownViewer, Func<MarkdownFindBarItem, string?>?>(nameof(FindBarLabels));

    private readonly List<(MarkdownTextBlock Text, int Start, int Length)> _findMatches = [];
    private FindBar? _findBar;
    private string? _findQuery;
    private bool _findMatchCase;
    private int _findCurrent = -1;
    private IInputElement? _focusBeforeFind;
    private ScrollViewer? _findScroller;

    /// <summary>Gets or sets the brush behind the matches of a find.</summary>
    public IBrush FindMatchBrush
    {
        get => GetValue(FindMatchBrushProperty);
        set => SetValue(FindMatchBrushProperty, value);
    }

    /// <summary>Gets or sets the brush behind the current match of a find.</summary>
    public IBrush FindCurrentMatchBrush
    {
        get => GetValue(FindCurrentMatchBrushProperty);
        set => SetValue(FindCurrentMatchBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the labels of the find bar: the placeholder of its text box and the accessible names and tooltips of
    /// its buttons. A <see langword="null"/> label keeps the English one.
    /// </summary>
    public Func<MarkdownFindBarItem, string?>? FindBarLabels
    {
        get => GetValue(FindBarLabelsProperty);
        set => SetValue(FindBarLabelsProperty, value);
    }

    /// <summary>Gets whether the find bar is open.</summary>
    public bool IsFindOpen => _findBar is { IsVisible: true };

    /// <summary>Gets the number of matches of the current find, or 0.</summary>
    public int FindMatchCount => _findMatches.Count;

    /// <summary>Gets the position of the current match among the matches, from 1, or 0 when there is none.</summary>
    public int CurrentFindMatch => _findCurrent + 1;

    /// <summary>The find bar, once it has been opened, for tests.</summary>
    internal FindBar? FindBarControl => _findBar;

    /// <summary>
    /// Opens the find bar at the top right of the view and moves the focus to it, as Ctrl+F (⌘F on macOS) in the viewer
    /// does: for a host that offers find from its own toolbar, menu or button. <paramref name="text"/> is put in the text
    /// box and found; without it, the text box keeps what it had, selected.
    /// </summary>
    public void OpenFind(string? text = null)
    {
        if (_findBar is null)
        {
            _findBar = new FindBar(this) { ZIndex = 1, IsVisible = false };
            LogicalChildren.Add(_findBar);
            VisualChildren.Add(_findBar);
        }
        if (!IsFindOpen)
        {
            _focusBeforeFind = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
        }
        _findBar.UpdateLook();
        _findBar.IsVisible = true;
        if (text is not null)
        {
            _findBar.Field.Text = text;
        }
        _findBar.Field.Focus(NavigationMethod.Tab);
        _findBar.Field.SelectAll();
        InvalidateMeasure();
    }

    /// <summary>Closes the find bar, takes the highlights away, and gives the focus back to where it was.</summary>
    public void CloseFind()
    {
        if (_findBar is null)
        {
            return;
        }
        // NOTE: the focus moves back before the bar hides: hiding the control with the focus moves it elsewhere.
        (_focusBeforeFind as InputElement)?.Focus(NavigationMethod.Unspecified);
        _focusBeforeFind = null;
        _findBar.IsVisible = false;
        ClearFind();
        _findQuery = null;
    }

    /// <summary>
    /// Finds <paramref name="text"/> in the viewer, highlights every match, and goes to the first one, scrolling it into
    /// view. Case is ignored unless <paramref name="matchCase"/>. A match never spans two blocks, but runs across emphasis
    /// and links, and finds an image by its alternative text and a formula by its LaTeX. Returns the number of matches.
    /// </summary>
    public int Find(string text, bool matchCase = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        _findQuery = text;
        _findMatchCase = matchCase;
        Collect();
        _findCurrent = _findMatches.Count > 0 ? 0 : -1;
        ShowMatches(bringIntoView: true);
        return _findMatches.Count;
    }

    /// <summary>Goes to the next match, after the last one to the first. Returns whether there is a match.</summary>
    public bool FindNext() => Step(1);

    /// <summary>Goes to the previous match, before the first one to the last. Returns whether there is a match.</summary>
    public bool FindPrevious() => Step(-1);

    private bool Step(int direction)
    {
        if (_findMatches.Count == 0)
        {
            return false;
        }
        _findCurrent = (_findCurrent + direction + _findMatches.Count) % _findMatches.Count;
        ShowMatches(bringIntoView: true);
        return true;
    }

    /// <summary>
    /// Finds the matches in every text in document order, those in a closed section too, which opens when its match is
    /// the current one.
    /// </summary>
    private void Collect()
    {
        foreach (var (text, _, _) in _findMatches)
        {
            text.SetFindMatches([], -1);
        }
        _findMatches.Clear();
        if (string.IsNullOrEmpty(_findQuery))
        {
            return;
        }
        foreach (var text in _panel.GetLogicalDescendants().OfType<MarkdownTextBlock>())
        {
            foreach (var (start, length) in text.Find(_findQuery, _findMatchCase))
            {
                _findMatches.Add((text, start, length));
            }
        }
    }

    private void ShowMatches(bool bringIntoView)
    {
        foreach (var group in _findMatches.Select((match, index) => (match, index)).GroupBy(entry => entry.match.Text))
        {
            var current = group.FirstOrDefault(entry => entry.index == _findCurrent);
            group.Key.SetFindMatches(
                [.. group.Select(entry => (entry.match.Start, entry.match.Length))],
                current.match.Text is null ? -1 : group.TakeWhile(entry => entry.index != _findCurrent).Count());
        }
        _findBar?.ShowCount(CurrentFindMatch, FindMatchCount);
        if (bringIntoView && _findCurrent >= 0)
        {
            BringMatchIntoView(_findMatches[_findCurrent]);
        }
    }

    /// <summary>
    /// Opens the closed sections around a match, then scrolls it into view with room above and below, so it does not
    /// sit at the very edge of the view.
    /// </summary>
    private void BringMatchIntoView((MarkdownTextBlock Text, int Start, int Length) match)
    {
        var opened = false;
        foreach (var expander in match.Text.GetLogicalAncestors().OfType<Expander>().Where(expander => !expander.IsExpanded))
        {
            expander.IsExpanded = true;
            opened = true;
        }
        void Scroll()
        {
            var bounds = match.Text.MatchBounds((match.Start, match.Length));
            var room = _findScroller is { Viewport.Height: > 0 } scroller ? scroller.Viewport.Height / 3 : 48;
            match.Text.BringIntoView(bounds.Inflate(new Thickness(0, room)));
        }
        if (opened)
        {
            Dispatcher.UIThread.Post(Scroll, DispatcherPriority.Loaded);
        }
        else
        {
            Scroll();
        }
    }

    private void ClearFind()
    {
        foreach (var (text, _, _) in _findMatches)
        {
            text.SetFindMatches([], -1);
        }
        _findMatches.Clear();
        _findCurrent = -1;
        _findBar?.ShowCount(0, 0);
    }

    /// <summary>
    /// Finds again after the text changed, as a streamed reply grows, keeping the current match where it can, without
    /// scrolling.
    /// </summary>
    private void KeepFind()
    {
        if (_findQuery is null)
        {
            return;
        }
        var current = _findCurrent;
        Collect();
        _findCurrent = _findMatches.Count == 0 ? -1 : Math.Clamp(current, 0, _findMatches.Count - 1);
        ShowMatches(bringIntoView: false);
    }

    /// <summary>Ctrl+F (⌘F on macOS) opens the find bar; F3 and Shift+F3 go through the matches.</summary>
    private void OnKeyDownForFind(object? sender, KeyEventArgs e)
    {
        var command = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        if (e.Key == Key.F && e.KeyModifiers == command)
        {
            var selected = SelectedText;
            OpenFind(selected.Length > 0 && !selected.Contains('\n', StringComparison.Ordinal) ? selected : null);
            e.Handled = true;
        }
        else if (e.Key == Key.F3 && _findMatches.Count > 0)
        {
            Step(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
            e.Handled = true;
        }
    }

    private void OnFindPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == FindMatchBrushProperty || change.Property == FindCurrentMatchBrushProperty)
        {
            ShowMatches(bringIntoView: false);
        }
        else if (_findBar is not null
            && (change.Property == FindBarLabelsProperty || change.Property == ThemeVariantScope.ActualThemeVariantProperty))
        {
            _findBar.UpdateLook();
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _findScroller = this.FindAncestorOfType<ScrollViewer>();
        if (_findScroller is not null)
        {
            _findScroller.ScrollChanged += OnScrollChangedForFind;
        }
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        if (_findScroller is not null)
        {
            _findScroller.ScrollChanged -= OnScrollChangedForFind;
            _findScroller = null;
        }
    }

    /// <summary>The find bar stays at the top of the view: it is placed by where the view is.</summary>
    private void OnScrollChangedForFind(object? sender, ScrollChangedEventArgs e)
    {
        if (IsFindOpen)
        {
            InvalidateArrange();
        }
    }

    private void MeasureFindBar()
    {
        if (IsFindOpen)
        {
            _findBar!.Measure(Size.Infinity);
        }
    }

    /// <summary>
    /// Places the find bar at the top right of the part of the viewer in view, so it stays there as the text scrolls in
    /// the nearest scroll viewer.
    /// </summary>
    private void ArrangeFindBar(Size finalSize)
    {
        if (!IsFindOpen)
        {
            return;
        }
        var top = 0.0;
        if (_findScroller is { Viewport.Height: > 0 } scroller && this.TranslatePoint(default, scroller) is { } origin)
        {
            top = Math.Clamp(-origin.Y, 0, Math.Max(0, finalSize.Height - _findBar!.DesiredSize.Height));
        }
        var size = _findBar!.DesiredSize;
        _findBar.Arrange(new Rect(Math.Max(0, finalSize.Width - size.Width - FindBarMargin), top + FindBarMargin, size.Width, size.Height));
    }

    private const double FindBarMargin = 8;
}
