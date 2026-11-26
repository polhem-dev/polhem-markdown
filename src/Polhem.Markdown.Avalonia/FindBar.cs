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
/// The find bar of a <see cref="MarkdownViewer"/>, floating at the top right of the view: a text box, the number of
/// matches, a button that matches case, buttons for the previous and the next match, and one that closes it. Enter goes
/// to the next match, Shift+Enter to the previous one, and Escape closes it.
/// </summary>
internal sealed class FindBar : Border
{
    // The icons are drawn on the same 16 by 16 grid, unscaled, so they share their size, weight and centre.
    private static readonly Geometry s_up = Geometry.Parse("M4 10 8 6 12 10");
    private static readonly Geometry s_down = Geometry.Parse("M4 6 8 10 12 6");
    private static readonly Geometry s_close = Geometry.Parse("M4.5 4.5 11.5 11.5M11.5 4.5 4.5 11.5");

    private const double ButtonSize = 28;
    private const double IconSize = 16;

    private readonly MarkdownViewer _viewer;
    private readonly TextBlock _count = new() { VerticalAlignment = VerticalAlignment.Center, MinWidth = 44, TextAlignment = TextAlignment.Center, Opacity = 0.7 };
    private readonly Dictionary<MarkdownFindBarItem, Control> _labelled = [];

    public FindBar(MarkdownViewer viewer)
    {
        _viewer = viewer;
        Padding = new Thickness(6, 4);
        CornerRadius = new CornerRadius(6);
        BorderThickness = new Thickness(1);
        BoxShadow = new BoxShadows(new BoxShadow { OffsetY = 2, Blur = 10, Color = Color.FromArgb(0x30, 0, 0, 0) });

        var matchCaseText = new TextBlock { Text = "Aa", FontSize = 12, FontWeight = FontWeight.SemiBold };
        MatchCase = Flat(new ToggleButton(), matchCaseText);

        // Off, it reads as the text around it; on, in the link color over the light background.
        void PaintMatchCase() => matchCaseText.Foreground = MatchCase.IsChecked == true
            ? _viewer.LinkForeground
            : _viewer.GetValue(global::Avalonia.Controls.Documents.TextElement.ForegroundProperty);
        MatchCase.IsCheckedChanged += (_, _) => PaintMatchCase();
        PaintMatchCase();
        Field = new TextBox { Width = 180, VerticalAlignment = VerticalAlignment.Center };
        Field.TextChanged += (_, _) => viewer.Find(Field.Text ?? "", MatchCase.IsChecked == true);
        MatchCase.IsCheckedChanged += (_, _) => viewer.Find(Field.Text ?? "", MatchCase.IsChecked == true);
        var previous = IconButton(s_up);
        previous.Click += (_, _) => viewer.FindPrevious();
        var next = IconButton(s_down);
        next.Click += (_, _) => viewer.FindNext();
        var close = IconButton(s_close);
        close.Click += (_, _) => viewer.CloseFind();

        _labelled[MarkdownFindBarItem.Field] = Field;
        _labelled[MarkdownFindBarItem.MatchCase] = MatchCase;
        _labelled[MarkdownFindBarItem.Previous] = previous;
        _labelled[MarkdownFindBarItem.Next] = next;
        _labelled[MarkdownFindBarItem.Close] = close;
        Child = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 2,
            Children = { Field, _count, MatchCase, previous, next, close },
        };
        UpdateLook();
    }

    public TextBox Field { get; }

    public ToggleButton MatchCase { get; }

    /// <summary>The number of matches shown, as "3/12", for tests.</summary>
    internal string CountText => _count.Text ?? "";

    /// <summary>Shows the current match and the number of matches: "3/12", or "0/0" when there are none.</summary>
    public void ShowCount(int current, int count) => _count.Text = count == 0 ? "0/0" : current + "/" + count;

    /// <summary>Gives the bar its labels, in the host's words where it gives them, and its colors.</summary>
    public void UpdateLook()
    {
        foreach (var (item, control) in _labelled)
        {
            var label = _viewer.FindBarLabels?.Invoke(item) ?? DefaultLabel(item);
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
        BorderBrush = _viewer.RuleBrush;
        Background = new SolidColorBrush(_viewer.IsDarkTheme ? Color.FromRgb(0x26, 0x26, 0x26) : Colors.White);
    }

    private static string DefaultLabel(MarkdownFindBarItem item) => item switch
    {
        MarkdownFindBarItem.Field => "Find",
        MarkdownFindBarItem.MatchCase => "Match case",
        MarkdownFindBarItem.Previous => "Previous match",
        MarkdownFindBarItem.Next => "Next match",
        _ => "Close",
    };

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
        path.Bind(global::Avalonia.Controls.Shapes.Shape.StrokeProperty, _viewer.GetObservable(global::Avalonia.Controls.Documents.TextElement.ForegroundProperty));
        return Flat(new Button(), path);
    }

    /// <summary>
    /// Draws a button of the bar flat, in the viewer's brushes instead of the theme's button, all of one size: a light
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
            ? _viewer.CodeBackground
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
        if (e.Key == Key.Enter)
        {
            if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                _viewer.FindPrevious();
            }
            else
            {
                _viewer.FindNext();
            }
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Escape)
        {
            _viewer.CloseFind();
            e.Handled = true;
            return;
        }
        base.OnKeyDown(e);
    }
}
