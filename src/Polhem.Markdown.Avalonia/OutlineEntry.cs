using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A heading in the table of contents: its text, indented by its level, which scrolls the viewer to the heading when
/// pressed. It is drawn with the viewer's brushes instead of the theme's button, so it looks the same as in the Blazor
/// viewer.
/// </summary>
internal sealed class OutlineEntry : Button
{
    private readonly Border _frame;
    private readonly TextBlock _text;
    private readonly MarkdownViewer _viewer;
    private bool _isCurrent;

    public OutlineEntry(MarkdownViewer viewer, MarkdownHeading heading)
    {
        _viewer = viewer;
        Heading = heading;
        _text = new TextBlock { Text = heading.Text, TextWrapping = TextWrapping.Wrap };
        _frame = new Border
        {
            Padding = new Thickness(8 + (Math.Clamp(heading.Level, 1, 6) - 1) * Indent, 4, 8, 4),
            CornerRadius = new CornerRadius(4),
            Background = Brushes.Transparent,
            Child = _text,
        };
        Template = new FuncControlTemplate<Button>((_, _) => _frame);
        HorizontalAlignment = global::Avalonia.Layout.HorizontalAlignment.Stretch;
        AutomationProperties.SetName(this, heading.Text);
        PointerEntered += (_, _) => Paint();
        PointerExited += (_, _) => Paint();
    }

    private const double Indent = 12;

    public MarkdownHeading Heading { get; }

    /// <summary>Whether this is the section being read, which is marked.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set
        {
            _isCurrent = value;
            _text.FontWeight = value ? FontWeight.SemiBold : FontWeight.Normal;
            AutomationProperties.SetItemStatus(this, value ? "current" : null);
            Paint();
        }
    }

    private void Paint() =>
        _frame.Background = _isCurrent || IsPointerOver ? _viewer.CodeBackground : Brushes.Transparent;
}
