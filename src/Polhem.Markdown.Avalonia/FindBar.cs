using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Path = Avalonia.Controls.Shapes.Path;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// The find bar of a <see cref="MarkdownViewer"/> or a <see cref="MarkdownEditor"/>, floating at the top right of the
/// view: a text box, the number of matches, a button that matches case, buttons for the previous and the next match, and
/// one that closes it. Enter goes to the next match, Shift+Enter to the previous one, and Escape closes it.
/// </summary>
/// <remarks>
/// In the editor it also has buttons for whole words and regular expressions, and a second row to replace: a text box,
/// and buttons that replace the current match and every match. Enter in that text box replaces the current match, and
/// the command shortcut with Enter (Ctrl, or Cmd on macOS) replaces every match.
/// </remarks>
internal sealed class FindBar : Border
{
    // The icons are drawn on the same 16 by 16 grid, unscaled, so they share their size, weight and centre.
    private static readonly Geometry s_up = Geometry.Parse("M4 10 8 6 12 10");
    private static readonly Geometry s_down = Geometry.Parse("M4 6 8 10 12 6");
    private static readonly Geometry s_close = Geometry.Parse("M4.5 4.5 11.5 11.5M11.5 4.5 4.5 11.5");
    private static readonly Geometry s_replace = Geometry.Parse("M2.5 8H9.5M7 5.5 9.5 8 7 10.5M12.5 4V12");
    private static readonly Geometry s_replaceAll = Geometry.Parse("M2.5 5.5H8.5M6.5 3.5 8.5 5.5 6.5 7.5M2.5 10.5H8.5M6.5 8.5 8.5 10.5 6.5 12.5M12.5 3V13");

    private const double ButtonSize = 28;
    private const double IconSize = 16;
    private const double FieldWidth = 180;

    private readonly IFindBarOwner _owner;
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 44, TextAlignment = TextAlignment.Center, Opacity = 0.7 };
    private readonly Dictionary<MarkdownFindBarItem, Control> _labelled = [];
    private readonly List<(ToggleButton Toggle, TextBlock Text)> _options = [];
    private readonly StackPanel? _replaceRow;
    private bool _settingQuery;

    public FindBar(IFindBarOwner owner, bool canReplace = false)
    {
        _owner = owner;
        Padding = new Thickness(6, 4);
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 10, Color = Color.FromArgb(0x30, 0, 0, 0) });

        Field = new TextBox { Width = FieldWidth, VerticalAlignment = VerticalAlignment.Center };
        Field.TextChanged += (_, _) => FindAgain();
        MatchCase = Option(new TextBlock { Text = "Aa" });
        var previous = IconButton(s_up);
        previous.Click += (_, _) => owner.FindPrevious();
        var next = IconButton(s_down);
        next.Click += (_, _) => owner.FindNext();
        var close = IconButton(s_close);
        close.Click += (_, _) => owner.CloseFind();

        _labelled[MarkdownFindBarItem.Field] = Field;
        _labelled[MarkdownFindBarItem.MatchCase] = MatchCase;
        _labelled[MarkdownFindBarItem.Previous] = previous;
        _labelled[MarkdownFindBarItem.Next] = next;
        _labelled[MarkdownFindBarItem.Close] = close;
        var findRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 2, Children = { Field, _count, MatchCase } };
        if (canReplace)
        {
            WholeWord = Option(new TextBlock { Text = "ab", TextDecorations = TextDecorations.Underline });
            RegularExpression = Option(new TextBlock { Text = ".*" });
            _labelled[MarkdownFindBarItem.WholeWord] = WholeWord;
            _labelled[MarkdownFindBarItem.RegularExpression] = RegularExpression;
            findRow.Children.Add(WholeWord);
            findRow.Children.Add(RegularExpression);

            ReplaceField = new TextBox { Width = FieldWidth, VerticalAlignment = VerticalAlignment.Center };
            var replace = IconButton(s_replace);
            replace.Click += (_, _) => owner.ReplaceFromBar(ReplaceField.Text ?? "", all: false);
            var replaceAll = IconButton(s_replaceAll);
            replaceAll.Click += (_, _) => owner.ReplaceFromBar(ReplaceField.Text ?? "", all: true);
            _labelled[MarkdownFindBarItem.ReplaceField] = ReplaceField;
            _labelled[MarkdownFindBarItem.Replace] = replace;
            _labelled[MarkdownFindBarItem.ReplaceAll] = replaceAll;
            _replaceRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                Margin = new Thickness(0, 4, 0, 0),
                IsVisible = false,
                Children = { ReplaceField, replace, replaceAll },
            };
        }
        findRow.Children.Add(previous);
        findRow.Children.Add(next);
        findRow.Children.Add(close);
        Child = _replaceRow is null ? findRow : new StackPanel { Children = { findRow, _replaceRow } };
        UpdateLook();
    }

    public TextBox Field { get; }

    public ToggleButton MatchCase { get; }

    /// <summary>The button for whole words, in the editor's bar only.</summary>
    public ToggleButton? WholeWord { get; }

    /// <summary>The button for regular expressions, in the editor's bar only.</summary>
    public ToggleButton? RegularExpression { get; }

    /// <summary>The text box of the replacement, in the editor's bar only.</summary>
    public TextBox? ReplaceField { get; }

    /// <summary>Gets or sets whether the row to replace shows, in the editor's bar.</summary>
    public bool ShowsReplace
    {
        get => _replaceRow is { IsVisible: true };
        set
        {
            if (_replaceRow is not null)
            {
                _replaceRow.IsVisible = value;
            }
        }
    }

    /// <summary>The options the buttons have on.</summary>
    public MarkdownFindOptions Options =>
        (MatchCase.IsChecked == true ? MarkdownFindOptions.MatchCase : MarkdownFindOptions.None)
        | (WholeWord?.IsChecked == true ? MarkdownFindOptions.WholeWord : MarkdownFindOptions.None)
        | (RegularExpression?.IsChecked == true ? MarkdownFindOptions.RegularExpression : MarkdownFindOptions.None);

    /// <summary>The number of matches shown, as "3/12", for tests.</summary>
    internal string CountText => _count.Text ?? "";

    /// <summary>Whether the field is marked as not a valid regular expression, for tests.</summary>
    internal bool ShowsInvalid { get; private set; }

    /// <summary>Shows the current match and the number of matches: "3/12", or "0/0" when there are none.</summary>
    public void ShowCount(int current, int count) => _count.Text = count == 0 ? "0/0" : current + "/" + count;

    /// <summary>Marks the field as not a valid regular expression, or takes the mark away.</summary>
    public void ShowInvalid(bool invalid)
    {
        ShowsInvalid = invalid;
        if (invalid)
        {
            Field.BorderBrush = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D));
        }
        else
        {
            Field.ClearValue(BorderBrushProperty);
        }
    }

    /// <summary>
    /// Puts <paramref name="text"/> and <paramref name="options"/> in the bar without finding again, for a find made from
    /// code while the bar is open.
    /// </summary>
    public void SetQuery(string text, MarkdownFindOptions options)
    {
        _settingQuery = true;
        try
        {
            Field.Text = text;
            MatchCase.IsChecked = options.HasFlag(MarkdownFindOptions.MatchCase);
            if (WholeWord is not null)
            {
                WholeWord.IsChecked = options.HasFlag(MarkdownFindOptions.WholeWord);
            }
            if (RegularExpression is not null)
            {
                RegularExpression.IsChecked = options.HasFlag(MarkdownFindOptions.RegularExpression);
            }
        }
        finally
        {
            _settingQuery = false;
        }
    }

    /// <summary>Gives the bar its labels, in the host's words where it gives them, and its colors.</summary>
    public void UpdateLook()
    {
        foreach (var (item, control) in _labelled)
        {
            var label = _owner.FindBarLabels?.Invoke(item) ?? DefaultLabel(item);
            AutomationProperties.SetName(control, label);
            if (control is TextBox field)
            {
                field.PlaceholderText = label;
            }
            else
            {
                ToolTip.SetTip(control, label);
            }
        }
        foreach (var option in _options)
        {
            PaintOption(option.Toggle, option.Text);
        }
        BorderBrush = _owner.FindBarBorderBrush;
        Background = new SolidColorBrush(_owner.IsDarkTheme ? Color.FromRgb(0x26, 0x26, 0x26) : Colors.White);
    }

    internal static string DefaultLabel(MarkdownFindBarItem item) => item switch
    {
        MarkdownFindBarItem.Field => "Find",
        MarkdownFindBarItem.MatchCase => "Match case",
        MarkdownFindBarItem.Previous => "Previous match",
        MarkdownFindBarItem.Next => "Next match",
        MarkdownFindBarItem.WholeWord => "Match whole word",
        MarkdownFindBarItem.RegularExpression => "Use regular expression",
        MarkdownFindBarItem.ReplaceField => "Replace",
        MarkdownFindBarItem.Replace => "Replace",
        MarkdownFindBarItem.ReplaceAll => "Replace all",
        _ => "Close",
    };

    private void FindAgain()
    {
        if (!_settingQuery)
        {
            _owner.FindFromBar(Field.Text ?? "", Options);
        }
    }

    /// <summary>A button that turns an option of the find on and off, shown as <paramref name="text"/>.</summary>
    private ToggleButton Option(TextBlock text)
    {
        text.FontSize = 12;
        text.FontWeight = FontWeight.SemiBold;
        var toggle = Flat(new ToggleButton(), text);
        _options.Add((toggle, text));

        // Off, it reads as the text around it; on, in the accent color over the light background.
        toggle.IsCheckedChanged += (_, _) =>
        {
            PaintOption(toggle, text);
            FindAgain();
        };
        return toggle;
    }

    private void PaintOption(ToggleButton toggle, TextBlock text) => text.Foreground = toggle.IsChecked == true
        ? _owner.FindBarAccentBrush
        : _owner.Control.GetValue(global::Avalonia.Controls.Documents.TextElement.ForegroundProperty);

    private Button IconButton(Geometry icon)
    {
        var path = new Path
        {
            Data = icon,
            Width = IconSize,
            Height = IconSize,
            Stretch = Stretch.None,
            StrokeThickness = 1.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };
        path.Bind(global::Avalonia.Controls.Shapes.Shape.StrokeProperty, _owner.Control.GetObservable(global::Avalonia.Controls.Documents.TextElement.ForegroundProperty));
        return Flat(new Button(), path);
    }

    /// <summary>
    /// Draws a button of the bar flat, in the owner's brushes instead of the theme's button, all of one size: a light
    /// background under the pointer, and a toggle that is on keeps it.
    /// </summary>
    private T Flat<T>(T button, Control content) where T : Button
    {
        var frame = new Border
        {
            Width = ButtonSize,
            Height = ButtonSize,
            CornerRadius = new CornerRadius(4),
            Background = Brushes.Transparent,
            Child = new Panel { Children = { content } },
        };
        content.HorizontalAlignment = HorizontalAlignment.Center;
        content.VerticalAlignment = VerticalAlignment.Center;
        button.Template = new global::Avalonia.Controls.Templates.FuncControlTemplate<T>((_, _) => frame);
        button.VerticalAlignment = VerticalAlignment.Center;
        button.Cursor = new Cursor(StandardCursorType.Hand);
        void Paint() => frame.Background = button.IsPointerOver || (button as ToggleButton)?.IsChecked == true
            ? _owner.FindBarHoverBrush
            : Brushes.Transparent;
        button.PointerEntered += (_, _) => Paint();
        button.PointerExited += (_, _) => Paint();
        if (button is ToggleButton toggle)
        {
            toggle.IsCheckedChanged += (_, _) => Paint();
        }
        return button;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter && ReplaceField is { IsKeyboardFocusWithin: true } replaceField)
        {
            var command = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
            _owner.ReplaceFromBar(replaceField.Text ?? "", all: e.KeyModifiers == command);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                _owner.FindPrevious();
            }
            else
            {
                _owner.FindNext();
            }
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            _owner.CloseFind();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
