using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Polhem.Markdown;
using Polhem.Markdown.Avalonia;

namespace MarkdownGallery;

/// <summary>
/// The sample text in a viewer, with a theme switch, a replay that types the text in chunks as a streamed reply
/// arrives, an edit switch that puts an editor beside the viewer, an outline of the headings that scrolls the viewer and
/// follows it, and a status line showing the last link to the host's own scheme.
/// </summary>
public sealed class GalleryView : UserControl
{
    private const int ReplayChunk = 12;

    private readonly MarkdownViewer _viewer;
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Opacity = 0.7 };
    private DispatcherTimer? _replay;
    private readonly ListBox _outline = new();

    /// <summary>Whether the outline's selection is being set to follow the scrolling, which must not scroll again.</summary>
    private bool _following;

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
        var showOutline = new ToggleSwitch { OnContent = "Outline", OffContent = "Outline", VerticalAlignment = VerticalAlignment.Center };

        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, Margin = new Thickness(16, 8) };
        bar.Children.Add(dark);
        bar.Children.Add(edit);
        bar.Children.Add(showOutline);
        bar.Children.Add(replay);
        bar.Children.Add(_status);

        var viewerScroll = new ScrollViewer { Content = new Border { Padding = new Thickness(16, 8, 16, 24), Child = _viewer } };
        var editor = new MarkdownEditor { Markdown = SampleText.Markdown };
        _ = new SourceSync(editor, _viewer, viewerScroll);

        // The outline lists the viewer's headings, scrolls the viewer to the one picked, and marks the one in view: the
        // last heading that starts at or before the source shown at the top.
        _outline.ItemsSource = _viewer.Headings;
        _outline.ItemTemplate = new FuncDataTemplate<MarkdownHeading>((heading, _) => new TextBlock
        {
            Text = heading?.Text,
            Margin = new Thickness(((heading?.Level ?? 1) - 1) * 12, 0, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        _viewer.HeadingsChanged += (_, _) => _outline.ItemsSource = _viewer.Headings;
        _outline.SelectionChanged += (_, _) =>
        {
            if (!_following && _outline.SelectedItem is MarkdownHeading heading)
            {
                _viewer.ScrollToHeading(heading.Id);
            }
        };
        viewerScroll.ScrollChanged += (_, _) => FollowScroll(viewerScroll);
        var outlineFrame = Frame(_outline);
        outlineFrame.Width = 220;
        outlineFrame.Margin = new Thickness(16, 0, 0, 16);
        outlineFrame.IsVisible = false;
        showOutline.IsCheckedChanged += (_, _) => outlineFrame.IsVisible = showOutline.IsChecked == true;

        // The editor sits beside the viewer, or above it on a narrow screen such as a phone's, each in a frame.
        var editorFrame = Frame(editor);
        var viewerFrame = Frame(viewerScroll);
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
        DockPanel.SetDock(outlineFrame, Dock.Left);
        layout.Children.Add(bar);
        layout.Children.Add(outlineFrame);
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

    private void FollowScroll(ScrollViewer viewerScroll)
    {
        // At the end of the text the last headings cannot reach the top of the view, so a heading picked there would give
        // way to the one above it at once; there the bottom of the view decides instead.
        var atEnd = viewerScroll.Offset.Y >= viewerScroll.Extent.Height - viewerScroll.Viewport.Height - 1;
        var position = atEnd ? viewerScroll.Viewport.Height : 0;
        if (_viewer.TranslatePoint(default, viewerScroll) is not { } origin
            || _viewer.GetSourceOffset(position - origin.Y) is not { } offset)
        {
            return;
        }
        _following = true;
        _outline.SelectedItem = _viewer.Headings.LastOrDefault(heading => heading.SourceOffset <= offset);
        _following = false;
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
