using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Shows a Markdown text. It renders the same model as the Blazor viewer of Polhem.Markdown, so both front ends show a
/// text the same way, and applies the same safety policy: raw HTML shows as text, images as their alternative text, and
/// only links whose scheme is in <see cref="Options"/> can be clicked.
/// </summary>
/// <remarks>
/// When <see cref="Markdown"/> changes, only the top-level blocks whose source changed are rebuilt, so a text that grows
/// while it arrives does not flicker. The colors work on light and dark themes; set <see cref="LinkForeground"/>,
/// <see cref="CodeBackground"/> and <see cref="RuleBrush"/> to match a theme.
/// </remarks>
public class MarkdownViewer : Control
{
    /// <summary>Defines the <see cref="Markdown"/> property.</summary>
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownViewer, string?>(nameof(Markdown));

    /// <summary>Defines the <see cref="Options"/> property.</summary>
    public static readonly StyledProperty<MarkdownOptions?> OptionsProperty =
        AvaloniaProperty.Register<MarkdownViewer, MarkdownOptions?>(nameof(Options));

    /// <summary>Defines the <see cref="LinkForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush> LinkForegroundProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(LinkForeground), new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)));

    /// <summary>Defines the <see cref="CodeBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeBackgroundProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(CodeBackground), new SolidColorBrush(Color.FromArgb(0x1F, 0x80, 0x80, 0x80)));

    /// <summary>Defines the <see cref="RuleBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> RuleBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(RuleBrush), new SolidColorBrush(Color.FromArgb(0x59, 0x80, 0x80, 0x80)));

    /// <summary>Defines the <see cref="CodeFontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<MarkdownViewer, FontFamily>(nameof(CodeFontFamily), new FontFamily("Cascadia Mono, Consolas, Menlo, monospace"));

    /// <summary>Defines the <see cref="CopyCodeLabel"/> property.</summary>
    public static readonly StyledProperty<string> CopyCodeLabelProperty =
        AvaloniaProperty.Register<MarkdownViewer, string>(nameof(CopyCodeLabel), "Copy");

    /// <summary>Defines the <see cref="LinkActivated"/> event, which bubbles.</summary>
    public static readonly RoutedEvent<MarkdownLinkActivatedEventArgs> LinkActivatedEvent =
        RoutedEvent.Register<MarkdownViewer, MarkdownLinkActivatedEventArgs>(nameof(LinkActivated), RoutingStrategies.Bubble);

    private readonly StackPanel _panel = new() { Spacing = 8 };
    private readonly List<string> _sources = [];

    /// <summary>Initializes a new instance of the <see cref="MarkdownViewer"/> class.</summary>
    public MarkdownViewer()
    {
        LogicalChildren.Add(_panel);
        VisualChildren.Add(_panel);
    }

    /// <summary>Gets or sets the Markdown text to show.</summary>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>
    /// Gets or sets the options, such as the link schemes that can be clicked. <see langword="null"/> uses the defaults
    /// of <see cref="MarkdownOptions"/>. Changing the set inside the same instance takes effect on the next change of
    /// <see cref="Markdown"/>; assign a new instance to apply it at once.
    /// </summary>
    public MarkdownOptions? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    /// <summary>Gets or sets the brush of link text.</summary>
    public IBrush LinkForeground
    {
        get => GetValue(LinkForegroundProperty);
        set => SetValue(LinkForegroundProperty, value);
    }

    /// <summary>Gets or sets the background of inline code and code blocks.</summary>
    public IBrush CodeBackground
    {
        get => GetValue(CodeBackgroundProperty);
        set => SetValue(CodeBackgroundProperty, value);
    }

    /// <summary>Gets or sets the brush of horizontal rules, table borders and the bar beside a quote.</summary>
    public IBrush RuleBrush
    {
        get => GetValue(RuleBrushProperty);
        set => SetValue(RuleBrushProperty, value);
    }

    /// <summary>Gets or sets the font of inline code and code blocks.</summary>
    public FontFamily CodeFontFamily
    {
        get => GetValue(CodeFontFamilyProperty);
        set => SetValue(CodeFontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the accessible name and tooltip of the copy button of a code block. Set it to translate the label.
    /// </summary>
    public string CopyCodeLabel
    {
        get => GetValue(CopyCodeLabelProperty);
        set => SetValue(CopyCodeLabelProperty, value);
    }

    /// <summary>
    /// Occurs when a user clicks a link. When no handler marks the event handled, the viewer asks the operating system
    /// to open the link.
    /// </summary>
    public event EventHandler<MarkdownLinkActivatedEventArgs>? LinkActivated
    {
        add => AddHandler(LinkActivatedEvent, value);
        remove => RemoveHandler(LinkActivatedEvent, value);
    }

    /// <summary>The node types the last full or partial rebuild rendered, for the node coverage test.</summary>
    internal IReadOnlySet<Type> RenderedNodeTypes { get; private set; } = new HashSet<Type>();

    /// <summary>The number of top-level blocks the last update built, as opposed to kept.</summary>
    internal int LastBuiltBlockCount { get; private set; }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MarkdownProperty)
        {
            Update(fullRebuild: false);
        }
        else if (change.Property == OptionsProperty || change.Property == TextElement.FontSizeProperty
            || change.Property == CodeFontFamilyProperty || change.Property == CopyCodeLabelProperty)
        {
            Update(fullRebuild: true);
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _panel.Measure(availableSize);
        return _panel.DesiredSize;
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        _panel.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <summary>
    /// Raises <see cref="LinkActivatedEvent"/> from <paramref name="source"/>, and opens the link when no handler
    /// handled it.
    /// </summary>
    internal void ActivateLink(Control source, Uri uri)
    {
        var args = new MarkdownLinkActivatedEventArgs(uri);
        source.RaiseEvent(args);
        if (!args.Handled && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    private void Update(bool fullRebuild)
    {
        var markdown = Markdown ?? "";
        var document = MarkdownModelBuilder.Build(markdown, Options ?? new MarkdownOptions());
        var renderer = new MarkdownRenderer(this);
        var built = 0;

        for (var index = 0; index < document.Blocks.Count; index++)
        {
            var block = document.Blocks[index];
            var source = markdown.Substring(block.Range.Start, block.Range.Length);

            // NOTE: a block is kept when its own source is unchanged. A link that refers to a reference definition
            // elsewhere in the text therefore keeps its old target when only that definition changes; replies write
            // links inline, and assigning Options rebuilds everything when it matters.
            if (!fullRebuild && index < _sources.Count && string.Equals(_sources[index], source, StringComparison.Ordinal))
            {
                continue;
            }

            var control = renderer.Block(block, depth: 0);
            if (index < _panel.Children.Count)
            {
                _panel.Children[index] = control;
                _sources[index] = source;
            }
            else
            {
                _panel.Children.Add(control);
                _sources.Add(source);
            }
            built++;
        }

        var extra = _panel.Children.Count - document.Blocks.Count;
        if (extra > 0)
        {
            _panel.Children.RemoveRange(document.Blocks.Count, extra);
            _sources.RemoveRange(document.Blocks.Count, extra);
        }

        RenderedNodeTypes = renderer.RenderedNodeTypes;
        LastBuiltBlockCount = built;
    }
}
