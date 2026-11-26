using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.VisualTree;
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

    /// <summary>Defines the <see cref="CjkFontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily?> CjkFontFamilyProperty =
        AvaloniaProperty.Register<MarkdownViewer, FontFamily?>(nameof(CjkFontFamily));

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

    /// <summary>Whether the text shows a picture with a source for a theme, so a change of theme changes what it shows.</summary>
    private bool _hasThemedImages;

    /// <summary>Initializes a new instance of the <see cref="MarkdownViewer"/> class.</summary>
    public MarkdownViewer()
    {
        LogicalChildren.Add(_panel);
        VisualChildren.Add(_panel);
        AddHandler(PointerPressedEvent, OnPointerPressedForSource, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressedForSelection, RoutingStrategies.Tunnel);
        AddHandler(PointerPressedEvent, OnPointerPressedAfterText, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerMovedEvent, OnPointerMovedForSelection, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerReleasedEvent, OnPointerReleasedForSelection, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(PointerCaptureLostEvent, OnPointerCaptureLostForSelection, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(KeyDownEvent, OnKeyDownForSelection, RoutingStrategies.Tunnel);
        AddHandler(SelectableTextBlock.CopyingToClipboardEvent, OnCopyingToClipboard, RoutingStrategies.Bubble);
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

    /// <summary>Gets or sets the brush of link text, which also fills a checked task box.</summary>
    public IBrush LinkForeground
    {
        get => GetValue(LinkForegroundProperty);
        set => SetValue(LinkForegroundProperty, value);
    }

    /// <summary>Gets or sets the background of inline code, keys (<c>&lt;kbd&gt;</c>) and code blocks.</summary>
    public IBrush CodeBackground
    {
        get => GetValue(CodeBackgroundProperty);
        set => SetValue(CodeBackgroundProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush of horizontal rules, table borders, the bar beside a quote and the border of an unchecked
    /// task box.
    /// </summary>
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

    /// <summary>Gets or sets the font of inline code, keys (<c>&lt;kbd&gt;</c>) and code blocks.</summary>
    public FontFamily CodeFontFamily
    {
        get => GetValue(CodeFontFamilyProperty);
        set => SetValue(CodeFontFamilyProperty, value);
    }

    /// <summary>
    /// Gets or sets the font family for Chinese, Japanese and Korean text, or <see langword="null"/>, the default, to draw
    /// it like any other text.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set it when bold or italic CJK text does not show as bold or italic. When the text's font has no CJK glyphs,
    /// Avalonia draws them in a fallback font, and within a line it reuses the fallback font it chose for earlier text
    /// together with that text's weight and style (<see href="https://github.com/AvaloniaUI/Avalonia/issues/18199"/>).
    /// A bold word after plain CJK text then shows plain, and plain text after a bold word shows bold.
    /// </para>
    /// <para>
    /// With this property set, the viewer draws the CJK characters of the text in this family, each with its own weight
    /// and style, and other characters keep their font. A list of names uses the first one installed, so one value can
    /// cover several platforms, such as <c>PingFang TC, Microsoft JhengHei UI, Noto Sans CJK TC</c>. Choose a family
    /// with the weights the text uses, a bold one in particular. Inline code and code blocks keep
    /// <see cref="CodeFontFamily"/>.
    /// </para>
    /// <para>
    /// The viewer also loads the faces of this family that are heavier than regular before it draws, because Avalonia
    /// matches a weight a family lacks, such as bold in PingFang TC, only among the faces it has already loaded.
    /// </para>
    /// </remarks>
    public FontFamily? CjkFontFamily
    {
        get => GetValue(CjkFontFamilyProperty);
        set => SetValue(CjkFontFamilyProperty, value);
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

    /// <summary>
    /// Gets the headings of the text, in document order, for a table of contents or an outline. It changes when
    /// <see cref="Markdown"/> or <see cref="Options"/> changes, which <see cref="HeadingsChanged"/> reports.
    /// </summary>
    /// <remarks>
    /// To follow the reader through the outline, compare each <see cref="MarkdownHeading.SourceOffset"/> with
    /// <see cref="GetSourceOffset"/> at the top of the view: the section at the top is that of the last heading that
    /// starts at or before it. Near the end of the text, the last headings may not reach the top of the view.
    /// </remarks>
    public IReadOnlyList<MarkdownHeading> Headings { get; private set; } = [];

    /// <summary>Occurs when <see cref="Headings"/> changes: a heading is added, removed, renamed or moved.</summary>
    public event EventHandler? HeadingsChanged;

    /// <summary>
    /// Scrolls the nearest scroll viewer so the heading with <paramref name="id"/> is at the top, or as near the top as the
    /// content allows, as following a link to it does. Returns whether the text has such a heading.
    /// </summary>
    /// <param name="id">A heading's <see cref="MarkdownHeading.Id"/>.</param>
    public bool ScrollToHeading(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return Headings.Any(heading => string.Equals(heading.Id, id, StringComparison.Ordinal)) && ScrollToAnchor(id);
    }

    /// <summary>The node types the last full or partial rebuild rendered, for the node coverage test.</summary>
    internal IReadOnlySet<Type> RenderedNodeTypes { get; private set; } = new HashSet<Type>();

    /// <summary>The diagram shape types the last full or partial rebuild drew, for the shape coverage test.</summary>
    internal IReadOnlySet<Type> RenderedShapeTypes { get; private set; } = new HashSet<Type>();

    /// <summary>The formula part types the last update laid out, for the formula coverage test.</summary>
    internal IReadOnlySet<Type> RenderedFormulaTypes { get; private set; } = new HashSet<Type>();

    /// <summary>The number of top-level blocks the last update built, as opposed to kept.</summary>
    internal int LastBuiltBlockCount { get; private set; }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // NOTE: a change of theme rebuilds only the blocks with a picture whose source depends on it, since the block key
        // holds the source shown.
        if (change.Property == MarkdownProperty
            || (change.Property == ThemeVariantScope.ActualThemeVariantProperty && _hasThemedImages))
        {
            Update(fullRebuild: false);
        }
        else if (change.Property == OptionsProperty || change.Property == TextElement.FontSizeProperty
            || change.Property == CodeFontFamilyProperty || change.Property == CjkFontFamilyProperty
            || change.Property == CopyCodeLabelProperty)
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
        // at the bottom. A rectangle as tall as the view starts at the target and fits only with its top at the top of the
        // view, whichever way the view scrolls. One taller than the view, such as the window's height when the scroll
        // viewer is shorter than the window, was aligned by its bottom edge when the view scrolled up, which left the
        // target above the view by the difference.
        var view = this.FindAncestorOfType<ScrollViewer>();
        var viewHeight = view is { Viewport.Height: > 0 } ? view.Viewport.Height : TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 0;
        var height = Math.Max(target.Bounds.Height, viewHeight);
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

    /// <summary>Whether the viewer's theme is dark, or derives from the dark one, which picks a picture's source.</summary>
    internal bool IsDarkTheme => ActualThemeVariant is { } theme
        && (theme == global::Avalonia.Styling.ThemeVariant.Dark || theme.InheritVariant == global::Avalonia.Styling.ThemeVariant.Dark);

    private void Update(bool fullRebuild)
    {
        var markdown = Markdown ?? "";
        var document = MarkdownModelBuilder.Build(markdown, Options ?? new MarkdownOptions());
        AssignDetailsKeys(document);
        if (CjkFontFamily is { } cjkFamily)
        {
            FontFaceWarmUp.LoadHeavierFaces(cjkFamily);
        }
        var renderer = new MarkdownRenderer(this);
        var keys = document.Blocks.Select(block => BlockKey(markdown.Substring(block.Range.Start, block.Range.Length), block)).ToList();

        // NOTE: a block is kept when its own source is unchanged. A link that refers to a reference definition
        // elsewhere in the text therefore keeps its old target when only that definition changes; replies write
        // links inline, and assigning Options rebuilds everything when it matters. Heading ids, footnote numbers and
        // anything else that depends on the rest of the text are part of the key (see `BlockKey`).
        //
        // The blocks that match from the start and from the end are kept, and only those between are rebuilt. A reply
        // that grows changes its last blocks, and an edit changes some in the middle; matching from both ends keeps a
        // block inserted or removed there from shifting every block after it out of step and rebuilding them all.
        var head = 0;
        var tail = 0;
        if (!fullRebuild)
        {
            var shorter = Math.Min(keys.Count, _sources.Count);
            while (head < shorter && string.Equals(_sources[head], keys[head], StringComparison.Ordinal))
            {
                head++;
            }
            while (tail < shorter - head
                && string.Equals(_sources[^(tail + 1)], keys[^(tail + 1)], StringComparison.Ordinal))
            {
                tail++;
            }
        }

        var built = keys.Count - head - tail;
        var controls = new List<Control>(built);
        for (var index = head; index < head + built; index++)
        {
            controls.Add(renderer.Block(document.Blocks[index], depth: 0));
        }
        var replaced = _sources.Count - head - tail;
        _panel.Children.RemoveRange(head, replaced);
        _panel.Children.InsertRange(head, controls);
        _sources.RemoveRange(head, replaced);
        _sources.InsertRange(head, keys.GetRange(head, built));

        KeepRanges(document);
        KeepSelection();
        KeepHeadings(document);
        ForgetImagesNotIn(document);
        _hasThemedImages = document.Blocks.SelectMany(NodeTree.SelfAndDescendants)
            .Any(node => node is ImageNode { DarkSource: not null } or ImageNode { LightSource: not null });
        RenderedNodeTypes = renderer.RenderedNodeTypes;
        RenderedShapeTypes = renderer.RenderedShapeTypes;
        RenderedFormulaTypes = renderer.RenderedFormulaTypes;
        LastBuiltBlockCount = built;
    }

    /// <summary>Keeps the headings of a rebuilt text, and reports a change in them.</summary>
    private void KeepHeadings(DocumentNode document)
    {
        if (Headings.SequenceEqual(document.Headings))
        {
            return;
        }
        Headings = document.Headings;
        HeadingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// The source of a top-level block, followed by what inside it depends on the rest of the text: heading ids, resolved
    /// links to headings, footnote numbers and ids, and image sources. A heading or a footnote definition that arrives later turns
    /// earlier text into a link, a heading added above another of the same name changes that one's id, and a reference
    /// added earlier renumbers the notes, all without the block's own source changing.
    /// </summary>
    private string BlockKey(string source, BlockNode block)
    {
        var anchors = NodeTree.SelfAndDescendants(block)
            .Select(node => node switch
            {
                HeadingNode heading => "#" + heading.Id,
                AnchorLinkNode link => ">" + link.Id,
                FootnoteReferenceNode reference => "^" + reference.Id + ">" + reference.NoteId + "=" + reference.Number,
                FootnoteNode note => "[" + note.Id + "=" + note.Number,
                FootnoteBackLinkNode back => "<" + back.ReferenceId + "=" + back.Position,
                // An image written by reference (`![a][r]`) takes its source from a definition elsewhere in the text.
                // A picture shows the source for the viewer's theme.
                ImageNode image => "!" + image.SourceFor(IsDarkTheme).AbsoluteUri,
                // A section's key depends on the sections before it with the same summary; its control holds the key.
                DetailsNode details => "?" + DetailsKey(details),
                _ => null,
            })
            .OfType<string>()
            .ToList();
        return anchors.Count == 0 ? source : source + "\0" + string.Join("\0", anchors);
    }
}
