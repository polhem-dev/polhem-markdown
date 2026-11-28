using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Polhem.Markdown.Avalonia;

// The find bar of the editor (polhem-dev/polhem-markdown#97), the viewer's bar with a row to replace: opening and closing
// it, its keys, and where it sits.
public partial class MarkdownEditor : IFindBarOwner
{
    /// <summary>Defines the <see cref="FindBarLabels"/> property, shared with <see cref="MarkdownViewer"/>.</summary>
    public static readonly StyledProperty<Func<MarkdownFindBarItem, string?>?> FindBarLabelsProperty = MarkdownViewer.FindBarLabelsProperty.AddOwner<MarkdownEditor>();

    /// <summary>Defines the <see cref="HandleFindShortcuts"/> property.</summary>
    public static readonly StyledProperty<bool> HandleFindShortcutsProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(HandleFindShortcuts), true);

    /// <summary>The background of a find bar button under the pointer: the viewer's default code background.</summary>
    private static readonly IBrush s_findBarHoverBrush = new SolidColorBrush(Color.FromArgb(0x1F, 0x80, 0x80, 0x80));

    private const double FindBarMargin = 8;

    /// <summary>
    /// Whether the find bar sits at the bottom of the text, where it moves when the current match is under it at the top
    /// and scrolling cannot uncover it (polhem-dev/polhem-markdown#108).
    /// </summary>
    private bool _findBarAtBottom;

    /// <summary>
    /// Gets or sets the labels of the find bar: the placeholders of its text boxes and the accessible names and tooltips
    /// of its buttons. A <see langword="null"/> label keeps the English one.
    /// </summary>
    public Func<MarkdownFindBarItem, string?>? FindBarLabels
    {
        get => GetValue(FindBarLabelsProperty);
        set => SetValue(FindBarLabelsProperty, value);
    }

    /// <summary>
    /// Gets or sets whether the editor opens find and replace on their keys itself. The default is <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// The keys are the command shortcut with F for find (Ctrl+F, or Cmd+F on macOS), Ctrl+H for replace (Option+Cmd+F
    /// on macOS), and F3 and Shift+F3 for the next and the previous match. Set it to <see langword="false"/> when the
    /// host's own menu or key bindings take these keys and call <see cref="OpenFind"/> and <see cref="OpenReplace"/>.
    /// Enter, Shift+Enter and Escape in the find bar work either way.
    /// </remarks>
    public bool HandleFindShortcuts
    {
        get => GetValue(HandleFindShortcutsProperty);
        set => SetValue(HandleFindShortcutsProperty, value);
    }

    /// <summary>Gets whether the find bar is open.</summary>
    public bool IsFindOpen => _findBar is { IsVisible: true };

    /// <summary>Gets whether the find bar is open with its row to replace.</summary>
    public bool IsReplaceOpen => IsFindOpen && _findBar!.ShowsReplace;

    /// <summary>The find bar, once it has been opened, for tests.</summary>
    internal FindBar? FindBarControl => _findBar;

    Control IFindBarOwner.Control => this;

    IBrush IFindBarOwner.FindBarBorderBrush => ToolbarBorderBrush;

    IBrush IFindBarOwner.FindBarHoverBrush => s_findBarHoverBrush;

    IBrush IFindBarOwner.FindBarAccentBrush => LinkForeground;

    bool IFindBarOwner.IsDarkTheme => ActualThemeVariant is { } theme
        && (theme == global::Avalonia.Styling.ThemeVariant.Dark || theme.InheritVariant == global::Avalonia.Styling.ThemeVariant.Dark);

    /// <summary>
    /// Opens the find bar at the top right of the text and moves the focus to it, as the command shortcut with F does:
    /// for a host that offers find from its own menu or button. <paramref name="text"/> is put in the text box and found;
    /// without it, the text box keeps what it had, selected.
    /// </summary>
    /// <remarks>
    /// When the current match is under the bar and scrolling cannot uncover it, as on the first lines of the text, the
    /// bar moves to the bottom right of the text, and stays there until a match is under it there or it closes.
    /// </remarks>
    /// <param name="text">The text to find, or <see langword="null"/> to keep the text box's.</param>
    public void OpenFind(string? text = null) => OpenBar(text, replace: false);

    /// <summary>
    /// Opens the find bar with its row to replace, as Ctrl+H (Option+Cmd+F on macOS) does. A read-only editor opens find
    /// only.
    /// </summary>
    /// <remarks>
    /// The focus goes to the replace text box when there is a text to find, and to the find text box otherwise.
    /// </remarks>
    /// <param name="text">The text to find, or <see langword="null"/> to keep the text box's.</param>
    public void OpenReplace(string? text = null) => OpenBar(text, replace: !IsReadOnly);

    /// <summary>
    /// Closes the find bar and takes the highlights of a find away; the selection stays on the match that was current.
    /// It also ends a find made from code with the bar closed.
    /// </summary>
    /// <remarks>The focus goes back to the text when it was in the bar.</remarks>
    public void CloseFind()
    {
        if (_findBar is { IsVisible: true } bar)
        {
            // NOTE: the focus moves back before the bar hides: hiding the control with the focus moves it elsewhere.
            if (bar.IsKeyboardFocusWithin)
            {
                _editor.TextArea.Focus();
            }
            bar.IsVisible = false;
            InvalidateMeasure();
        }
        _findBarAtBottom = false;
        _finder = null;
        _findQuery = null;
        _findMatches.Clear();
        _findCurrent = -1;
        ShowMatches(select: false);
    }

    void IFindBarOwner.FindFromBar(string text, MarkdownFindOptions options)
    {
        TextFinder finder;
        try
        {
            finder = new TextFinder(text, options);
        }
        catch (ArgumentException)
        {
            // A regular expression half typed is not valid yet: the bar says so, and nothing is highlighted.
            _findBar?.ShowInvalid(true);
            _finder = null;
            _findQuery = text;
            _findOptions = options;
            _findMatches.Clear();
            _findCurrent = -1;
            ShowMatches(select: false);
            return;
        }
        _findBar?.ShowInvalid(false);
        Run(finder, text, options);
    }

    void IFindBarOwner.ReplaceFromBar(string replacement, bool all)
    {
        if (all)
        {
            ReplaceAllMatches(replacement);
        }
        else
        {
            ReplaceCurrentMatch(replacement);
        }
    }

    private void OpenBar(string? text, bool replace)
    {
        if (_findBar is null)
        {
            _findBar = new FindBar(this, canReplace: true) { ZIndex = 1, IsVisible = false };
            LogicalChildren.Add(_findBar);
            VisualChildren.Add(_findBar);
        }
        var bar = _findBar;
        bar.UpdateLook();
        bar.ShowsReplace = replace;
        bar.IsVisible = true;
        if (text is not null)
        {
            bar.Field.Text = text;
        }
        else if (_findQuery is not null)
        {
            // A find made from code before the bar opened: the bar shows it.
            bar.SetQuery(_findQuery, _findOptions);
        }
        if (_findQuery is null && !string.IsNullOrEmpty(bar.Field.Text))
        {
            // Opened again after it closed: the text box still has the last find, so its matches show again.
            ((IFindBarOwner)this).FindFromBar(bar.Field.Text, bar.Options);
        }
        if (replace && !string.IsNullOrEmpty(bar.Field.Text))
        {
            bar.ReplaceField!.Focus(NavigationMethod.Tab);
            bar.ReplaceField.SelectAll();
        }
        else
        {
            bar.Field.Focus(NavigationMethod.Tab);
            bar.Field.SelectAll();
        }
        InvalidateMeasure();
    }

    /// <summary>
    /// The command shortcut with F opens find, Ctrl+H (Option+Cmd+F on macOS) opens replace, and F3 and Shift+F3 go
    /// through the matches; the selection, on one line, becomes the text to find.
    /// </summary>
    private void OnKeyDownForFind(object? sender, KeyEventArgs e)
    {
        if (!HandleFindShortcuts)
        {
            return;
        }
        var command = CommandModifiers();
        var mac = command == KeyModifiers.Meta;
        if (e.Key == Key.F && e.KeyModifiers == command)
        {
            OpenFind(SelectionAsQuery());
            e.Handled = true;
        }
        else if (mac ? e.Key == Key.F && e.KeyModifiers == (KeyModifiers.Meta | KeyModifiers.Alt) : e.Key == Key.H && e.KeyModifiers == KeyModifiers.Control)
        {
            OpenReplace(SelectionAsQuery());
            e.Handled = true;
        }
        else if (e.Key == Key.F3 && e.KeyModifiers is KeyModifiers.None or KeyModifiers.Shift && _findMatches.Count > 0)
        {
            Step(e.KeyModifiers == KeyModifiers.Shift ? -1 : 1);
            e.Handled = true;
        }
    }

    /// <summary>
    /// The selection as a text to find, written as a regular expression when the bar finds those, or
    /// <see langword="null"/> when nothing or more than one line is selected.
    /// </summary>
    private string? SelectionAsQuery()
    {
        var selected = _editor.SelectedText;
        if (selected.Length == 0 || selected.Contains('\n', StringComparison.Ordinal))
        {
            return null;
        }
        return _findBar?.RegularExpression?.IsChecked == true ? Regex.Escape(selected) : selected;
    }

    private void OnFindPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property == FindMatchBrushProperty || change.Property == FindCurrentMatchBrushProperty)
        {
            ShowMatches(select: false);
        }
        else if (change.Property == IsReadOnlyProperty && IsReadOnly && _findBar is not null)
        {
            _findBar.ShowsReplace = false;
        }
        else if (_findBar is not null
            && (change.Property == FindBarLabelsProperty
                || change.Property == ToolbarBorderBrushProperty
                || change.Property == LinkForegroundProperty
                || change.Property == ThemeVariantScope.ActualThemeVariantProperty))
        {
            _findBar.UpdateLook();
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
    /// Places the find bar at the top right of the text, below the toolbar and left of the vertical scroll bar, or at the
    /// bottom right, above the horizontal scroll bar, when the current match was under it at the top.
    /// </summary>
    private void ArrangeFindBar(Size finalSize, double top)
    {
        if (!IsFindOpen)
        {
            return;
        }
        var size = _findBar!.DesiredSize;
        var textView = _editor.TextArea.TextView;
        var corner = textView.TranslatePoint(new Point(textView.Bounds.Width, textView.Bounds.Height), this)
            ?? new Point(finalSize.Width, finalSize.Height);
        var y = _findBarAtBottom ? Math.Max(top, corner.Y - size.Height - FindBarMargin) : top + FindBarMargin;
        _findBar.Arrange(new Rect(Math.Max(0, corner.X - size.Width - FindBarMargin), y, size.Width, size.Height));
    }
}
