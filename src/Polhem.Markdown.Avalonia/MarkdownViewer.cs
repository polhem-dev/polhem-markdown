using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Shows a Markdown text. It renders the same model as the Blazor viewer of Polhem.Markdown, so both front ends show a
/// text the same way, and applies the same safety policy: raw HTML shows as text, only images and links whose scheme is in
/// <see cref="Options"/> load or can be clicked, and other images show their alternative text.
/// </summary>
/// <remarks>
/// When <see cref="Markdown"/> changes, only the top-level blocks whose source changed are rebuilt, so a text that grows
/// while it arrives does not flicker. The colors work on light and dark themes; set <see cref="LinkForeground"/>,
/// <see cref="CodeBackground"/>, <see cref="RuleBrush"/>, the alert brushes (such as <see cref="AlertNoteBrush"/>) and the
/// code brushes (such as <see cref="CodeKeywordBrush"/>) to match a theme.
/// </remarks>
public partial class MarkdownViewer : Control
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

    /// <summary>Defines the <see cref="AlertNoteBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> AlertNoteBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(AlertNoteBrush), new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)));

    /// <summary>Defines the <see cref="AlertTipBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> AlertTipBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(AlertTipBrush), new SolidColorBrush(Color.FromRgb(0x22, 0xA3, 0x5A)));

    /// <summary>Defines the <see cref="AlertImportantBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> AlertImportantBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(AlertImportantBrush), new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)));

    /// <summary>Defines the <see cref="AlertWarningBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> AlertWarningBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(AlertWarningBrush), new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)));

    /// <summary>Defines the <see cref="AlertCautionBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> AlertCautionBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(AlertCautionBrush), new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)));

    /// <summary>Defines the <see cref="CodeKeywordBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeKeywordBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(CodeKeywordBrush), new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)));

    /// <summary>Defines the <see cref="CodeStringBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeStringBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(CodeStringBrush), new SolidColorBrush(Color.FromRgb(0x22, 0xA3, 0x5A)));

    /// <summary>Defines the <see cref="CodeCommentBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeCommentBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(CodeCommentBrush), new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98)));

    /// <summary>Defines the <see cref="CodeConstantBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeConstantBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(CodeConstantBrush), new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)));

    /// <summary>Defines the <see cref="CodeTagBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeTagBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(CodeTagBrush), new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)));

    /// <summary>Defines the <see cref="CodePropertyBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodePropertyBrushProperty =
        AvaloniaProperty.Register<MarkdownViewer, IBrush>(nameof(CodePropertyBrush), new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)));

    /// <summary>Defines the <see cref="CodeFontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<MarkdownViewer, FontFamily>(nameof(CodeFontFamily), new FontFamily("Cascadia Mono, Consolas, Menlo, monospace"));

    /// <summary>Defines the <see cref="CopyCodeLabel"/> property.</summary>
    public static readonly StyledProperty<string> CopyCodeLabelProperty =
        AvaloniaProperty.Register<MarkdownViewer, string>(nameof(CopyCodeLabel), "Copy");

    /// <summary>Defines the <see cref="LinkActivated"/> event, which bubbles.</summary>
    public static readonly RoutedEvent<MarkdownLinkActivatedEventArgs> LinkActivatedEvent =
        RoutedEvent.Register<MarkdownViewer, MarkdownLinkActivatedEventArgs>(nameof(LinkActivated), RoutingStrategies.Bubble);

    /// <summary>
    /// The ids a link in the text moves to a control by: a heading's, a footnote's, or those of the footnote references in
    /// a paragraph.
    /// </summary>
    internal static readonly AttachedProperty<IReadOnlyList<string>> AnchorIdsProperty =
        AvaloniaProperty.RegisterAttached<MarkdownViewer, Control, IReadOnlyList<string>>("AnchorIds", []);

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

    /// <summary>Gets or sets the brush of the bar, icon and title of a note (<c>&gt; [!NOTE]</c>).</summary>
    public IBrush AlertNoteBrush
    {
        get => GetValue(AlertNoteBrushProperty);
        set => SetValue(AlertNoteBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of the bar, icon and title of a tip (<c>&gt; [!TIP]</c>).</summary>
    public IBrush AlertTipBrush
    {
        get => GetValue(AlertTipBrushProperty);
        set => SetValue(AlertTipBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of the bar, icon and title of an important alert (<c>&gt; [!IMPORTANT]</c>).</summary>
    public IBrush AlertImportantBrush
    {
        get => GetValue(AlertImportantBrushProperty);
        set => SetValue(AlertImportantBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of the bar, icon and title of a warning (<c>&gt; [!WARNING]</c>).</summary>
    public IBrush AlertWarningBrush
    {
        get => GetValue(AlertWarningBrushProperty);
        set => SetValue(AlertWarningBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of the bar, icon and title of a caution (<c>&gt; [!CAUTION]</c>).</summary>
    public IBrush AlertCautionBrush
    {
        get => GetValue(AlertCautionBrushProperty);
        set => SetValue(AlertCautionBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of keywords in a highlighted code block.</summary>
    public IBrush CodeKeywordBrush
    {
        get => GetValue(CodeKeywordBrushProperty);
        set => SetValue(CodeKeywordBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of strings in a highlighted code block.</summary>
    public IBrush CodeStringBrush
    {
        get => GetValue(CodeStringBrushProperty);
        set => SetValue(CodeStringBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of comments in a highlighted code block.</summary>
    public IBrush CodeCommentBrush
    {
        get => GetValue(CodeCommentBrushProperty);
        set => SetValue(CodeCommentBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of numbers and constants such as <c>true</c> and <c>null</c> in a highlighted code block.</summary>
    public IBrush CodeConstantBrush
    {
        get => GetValue(CodeConstantBrushProperty);
        set => SetValue(CodeConstantBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of markup tag names in a highlighted code block.</summary>
    public IBrush CodeTagBrush
    {
        get => GetValue(CodeTagBrushProperty);
        set => SetValue(CodeTagBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of attribute names, JSON and YAML keys, CSS properties and shell variables in a highlighted code block.</summary>
    public IBrush CodePropertyBrush
    {
        get => GetValue(CodePropertyBrushProperty);
        set => SetValue(CodePropertyBrushProperty, value);
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

    /// <summary>The diagram shape types the last full or partial rebuild drew, for the shape coverage test.</summary>
    internal IReadOnlySet<Type> RenderedShapeTypes { get; private set; } = new HashSet<Type>();

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
    /// Scrolls the nearest scroll viewer so the control with <paramref name="anchorId"/> is at the top, or as near the top
    /// as the content allows. Returns whether the viewer shows such a control.
    /// </summary>
    internal bool ScrollToAnchor(string anchorId)
    {
        var target = _panel.GetLogicalDescendants()
            .OfType<Control>()
            .FirstOrDefault(control => control.GetValue(AnchorIdsProperty).Contains(anchorId, StringComparer.Ordinal));
        if (target is null)
        {
            return false;
        }

        // NOTE: a scroll viewer brings a rectangle into view by the least movement, which leaves a target below the view
        // at the bottom. A rectangle as tall as the window starts at the target and cannot fit, and the scroll viewer
        // then aligns its top edge.
        var height = Math.Max(target.Bounds.Height, TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 0);
        target.BringIntoView(new Rect(0, 0, target.Bounds.Width, height));
        return true;
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
            var source = BlockKey(markdown.Substring(block.Range.Start, block.Range.Length), block);

            // NOTE: a block is kept when its own source is unchanged. A link that refers to a reference definition
            // elsewhere in the text therefore keeps its old target when only that definition changes; replies write
            // links inline, and assigning Options rebuilds everything when it matters. Heading ids and links to
            // headings do depend on the rest of the text, so they are part of the key (see `BlockKey`).
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

        ForgetImagesNotIn(document);
        RenderedNodeTypes = renderer.RenderedNodeTypes;
        RenderedShapeTypes = renderer.RenderedShapeTypes;
        LastBuiltBlockCount = built;
    }

    /// <summary>
    /// The source of a top-level block, followed by what inside it depends on the rest of the text: heading ids, resolved
    /// links to headings, and footnote numbers and ids. A heading or a footnote definition that arrives later turns
    /// earlier text into a link, a heading added above another of the same name changes that one's id, and a reference
    /// added earlier renumbers the notes, all without the block's own source changing.
    /// </summary>
    private static string BlockKey(string source, BlockNode block)
    {
        var anchors = NodeTree.SelfAndDescendants(block)
            .Select(node => node switch
            {
                HeadingNode heading => "#" + heading.Id,
                AnchorLinkNode link => ">" + link.Id,
                FootnoteReferenceNode reference => "^" + reference.Id + ">" + reference.NoteId + "=" + reference.Number,
                FootnoteNode note => "[" + note.Id + "=" + note.Number,
                FootnoteBackLinkNode back => "<" + back.ReferenceId + "=" + back.Position,
                _ => null,
            })
            .OfType<string>()
            .ToList();
        return anchors.Count == 0 ? source : source + "\0" + string.Join("\0", anchors);
    }
}
