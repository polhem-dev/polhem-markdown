using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Polhem.Markdown.Model;
using Path = Avalonia.Controls.Shapes.Path;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A code block: a header with the language and a copy button, and the code in a monospaced font that scrolls
/// sideways instead of wrapping.
/// </summary>
internal sealed class CodeBlockView : Border
{
    private static readonly Geometry s_copyIcon = Geometry.Parse(
        "M8 2h9a2 2 0 0 1 2 2v11h-2V4H8V2zM5 6h9a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2zm0 2v12h9V8H5z");

    private static readonly Geometry s_doneIcon = Geometry.Parse("M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z");

    private readonly Path _icon;
    private IDisposable? _resetTimer;

    public CodeBlockView(MarkdownViewer viewer, CodeBlockNode code)
    {
        Code = code.Code;
        CornerRadius = new CornerRadius(4);
        Bind(BackgroundProperty, viewer.GetObservable(MarkdownViewer.CodeBackgroundProperty));

        var language = new TextBlock
        {
            Text = code.Language,
            Opacity = 0.7,
            FontSize = viewer.GetValue(TextElement.FontSizeProperty) * 0.85,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _icon = new Path { Data = s_copyIcon, Width = 14, Height = 14, Stretch = Stretch.Uniform };
        _icon.Bind(Shape.FillProperty, viewer.GetObservable(TextElement.ForegroundProperty));
        CopyButton = new Button
        {
            Content = _icon,
            Padding = new Thickness(6, 4),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(CopyButton, viewer.CopyCodeLabel);
        ToolTip.SetTip(CopyButton, viewer.CopyCodeLabel);
        CopyButton.Click += async (_, _) => await CopyAsync();

        var header = new Grid { Margin = new Thickness(10, 2, 2, 0) };
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        header.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        header.Children.Add(language);
        Grid.SetColumn(CopyButton, 1);
        header.Children.Add(CopyButton);

        var body = new SelectableTextBlock { Text = code.Code, TextWrapping = TextWrapping.NoWrap };
        body.Bind(TextBlock.FontFamilyProperty, viewer.GetObservable(MarkdownViewer.CodeFontFamilyProperty));
        var scroller = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Padding = new Thickness(10, 0, 10, 8),
            Content = body,
        };

        var layout = new DockPanel();
        DockPanel.SetDock(header, Dock.Top);
        layout.Children.Add(header);
        layout.Children.Add(scroller);
        Child = layout;
    }

    /// <summary>The code the copy button copies.</summary>
    public string Code { get; }

    public Button CopyButton { get; }

    /// <summary>Puts the code on the clipboard and shows a check mark on the button for a moment.</summary>
    internal async Task CopyAsync()
    {
        if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard)
        {
            return;
        }
        await clipboard.SetTextAsync(Code);

        _icon.Data = s_doneIcon;
        _resetTimer?.Dispose();
        _resetTimer = DispatcherTimer.RunOnce(() => _icon.Data = s_copyIcon, TimeSpan.FromSeconds(1.5));
    }
}
