using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Polhem.Markdown;
using Polhem.Markdown.Avalonia;

namespace MarkdownGallery;

/// <summary>
/// The sample text in a viewer, with a theme switch, a replay that types the text in chunks as a streamed reply
/// arrives, an edit switch that puts an editor beside the viewer, and a status line showing the last link to the host's
/// own scheme.
/// </summary>
public sealed class GalleryView : UserControl
{
    private const int ReplayChunk = 12;

    private readonly MarkdownViewer _viewer;
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
    private DispatcherTimer? _replay;

    public GalleryView()
    {
        var options = new MarkdownOptions();
        options.AllowedLinkSchemes.Add("polhem");
        _viewer = new MarkdownViewer { Options = options, Markdown = SampleText.Markdown };
        _viewer.LinkActivated += OnLinkActivated;

        var dark = new ToggleSwitch { OnContent = "Dark", OffContent = "Light", VerticalAlignment = VerticalAlignment.Center };
        dark.IsCheckedChanged += (_, _) =>
            Application.Current!.RequestedThemeVariant = dark.IsChecked == true ? ThemeVariant.Dark : ThemeVariant.Light;

        var replay = new Button { Content = "Replay", VerticalAlignment = VerticalAlignment.Center };
        replay.Click += (_, _) => Replay();

        var edit = new ToggleSwitch { OnContent = "Edit", OffContent = "Edit", VerticalAlignment = VerticalAlignment.Center };

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(16, 8) };
        bar.Children.Add(dark);
        bar.Children.Add(edit);
        bar.Children.Add(replay);
        bar.Children.Add(_status);

        var viewerScroll = new ScrollViewer { Content = new Border { Padding = new Thickness(16, 8, 16, 24), Child = _viewer } };
        var editor = new MarkdownEditor { Markdown = SampleText.Markdown, IsVisible = false, Margin = new Thickness(8, 0, 0, 0) };
        _ = new SourceSync(editor, _viewer, viewerScroll);

        // The editor sits beside the viewer, or above it on a narrow screen such as a phone's.
        var panes = new Grid();
        panes.Children.Add(editor);
        panes.Children.Add(viewerScroll);
        void Arrange()
        {
            var wide = panes.Bounds.Width >= 700;
            panes.ColumnDefinitions = new ColumnDefinitions(editor.IsVisible && wide ? "*,*" : "*");
            panes.RowDefinitions = new RowDefinitions(editor.IsVisible && !wide ? "*,*" : "*");
            Grid.SetColumn(viewerScroll, editor.IsVisible && wide ? 1 : 0);
            Grid.SetRow(viewerScroll, editor.IsVisible && !wide ? 1 : 0);
        }
        panes.SizeChanged += (_, _) => Arrange();
        edit.IsCheckedChanged += (_, _) =>
        {
            editor.IsVisible = edit.IsChecked == true;
            replay.IsEnabled = !editor.IsVisible;
            Arrange();
        };

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        layout.Children.Add(bar);
        layout.Children.Add(panes);
        Content = layout;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this)?.InsetsManager is { } insets)
        {
            Padding = insets.SafeAreaPadding;
            insets.SafeAreaChanged += (_, args) => Padding = args.SafeAreaPadding;
        }
    }

    private void OnLinkActivated(object? sender, MarkdownLinkActivatedEventArgs e)
    {
        // Links to the host's own scheme are handled here; any other link is left to the viewer, which opens it.
        if (e.Uri.Scheme == "polhem")
        {
            _status.Text = "Opened " + e.Uri;
            e.Handled = true;
        }
    }

    private void Replay()
    {
        _replay?.Stop();
        var shown = 0;
        _viewer.Markdown = "";
        _replay = DispatcherTimer.Run(() =>
        {
            shown = Math.Min(shown + ReplayChunk, SampleText.Markdown.Length);
            _viewer.Markdown = SampleText.Markdown[..shown];
            return shown < SampleText.Markdown.Length;
        }, TimeSpan.FromMilliseconds(30)) as DispatcherTimer;
    }
}
