using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Styling;
using Avalonia.Threading;
using Polhem.Markdown;
using Polhem.Markdown.Avalonia;

namespace MarkdownGallery;

/// <summary>
/// The sample text in a viewer inside a reader, which adds the table of contents, with a theme switch, a replay that
/// types the text in chunks as a streamed reply arrives, an edit switch that puts an editor beside the viewer, and a
/// status line showing the last link to the host's own scheme.
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
        _viewer = new MarkdownViewer { Options = options, Markdown = SampleText.Markdown, ZoomImages = true };
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

        // The reader scrolls the viewer and offers the table of contents along its edge.
        var reader = new MarkdownReader { Viewer = _viewer };
        var viewerScroll = reader.ScrollViewer;
        var editor = new MarkdownEditor { Markdown = SampleText.Markdown };
        _ = new SourceSync(editor, _viewer, viewerScroll);

        // The editor sits beside the viewer, or above it on a narrow screen such as a phone's, each in a frame.
        var editorFrame = Frame(editor);
        var viewerFrame = Frame(reader);
        editorFrame.IsVisible = false;
        var panes = new Grid { Margin = new Thickness(16, 0, 16, 16) };
        panes.Children.Add(editorFrame);
        panes.Children.Add(viewerFrame);
        void Arrange()
        {
            var editing = editorFrame.IsVisible;
            var wide = panes.Bounds.Width >= 700;
            panes.ColumnDefinitions = ColumnDefinitions.Parse(editing && wide ? "*,*" : "*");
            panes.RowDefinitions = RowDefinitions.Parse(editing && !wide ? "*,*" : "*");
            Grid.SetColumn(viewerFrame, editing && wide ? 1 : 0);
            Grid.SetRow(viewerFrame, editing && !wide ? 1 : 0);
            editorFrame.Margin = new Thickness(0, 0, editing && wide ? 8 : 0, editing && !wide ? 8 : 0);
            viewerFrame.Margin = new Thickness(editing && wide ? 8 : 0, editing && !wide ? 8 : 0, 0, 0);
        }
        panes.SizeChanged += (_, _) => Arrange();
        edit.IsCheckedChanged += (_, _) =>
        {
            editorFrame.IsVisible = edit.IsChecked == true;
            replay.IsEnabled = !editorFrame.IsVisible;
            Arrange();
        };

        var layout = new DockPanel();
        DockPanel.SetDock(bar, Dock.Top);
        layout.Children.Add(bar);
        layout.Children.Add(panes);
        Content = layout;
    }

    /// <summary>A rounded frame in the theme's border colour, which clips its content to its corners.</summary>
    private static Border Frame(Control content)
    {
        var frame = new Border
        {
            Child = content,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
        };
        frame.Bind(Border.BorderBrushProperty, frame.GetResourceObservable("SemiColorBorder"));
        return frame;
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

        // While the reply arrives, the viewer closes the syntax it leaves open; at its end, the text shows as written.
        _viewer.IsStreaming = true;
        _replay = DispatcherTimer.Run(() =>
        {
            shown = Math.Min(shown + ReplayChunk, SampleText.Markdown.Length);
            _viewer.Markdown = SampleText.Markdown[..shown];
            _viewer.IsStreaming = shown < SampleText.Markdown.Length;
            return _viewer.IsStreaming;
        }, TimeSpan.FromMilliseconds(30)) as DispatcherTimer;
    }
}
