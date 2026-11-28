using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Edits a Markdown text as source, coloured the way <see cref="MarkdownViewer"/> reads it: markers, headings, emphasis,
/// code, links, raw HTML and formulas, and the code of fenced blocks with the viewer's highlighting.
/// </summary>
/// <remarks>
/// <para>
/// It works under any application theme and needs no styles from the host. The colours work on light and dark themes; set
/// <see cref="HeadingBrush"/>, <see cref="MarkerBrush"/>, <see cref="LinkForeground"/> and the other brushes to match a
/// theme. Links in the source are not clickable: a link is opened from the viewer, where its scheme is checked.
/// </para>
/// <para>
/// <see cref="Markdown"/> follows every edit and binds two-way by default. The colouring follows a moment after typing
/// stops, from a scan on a background thread; until then the colours move with the edits.
/// </para>
/// </remarks>
public partial class MarkdownEditor : Control
{
    /// <summary>Defines the <see cref="Markdown"/> property.</summary>
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<MarkdownEditor, string?>(nameof(Markdown), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>Defines the <see cref="IsReadOnly"/> property.</summary>
    public static readonly StyledProperty<bool> IsReadOnlyProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(IsReadOnly));

    /// <summary>Defines the <see cref="ShowLineNumbers"/> property.</summary>
    public static readonly StyledProperty<bool> ShowLineNumbersProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(ShowLineNumbers), true);

    /// <summary>Defines the <see cref="WordWrap"/> property.</summary>
    public static readonly StyledProperty<bool> WordWrapProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(WordWrap), true);

    /// <summary>Defines the <see cref="CodeFontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> CodeFontFamilyProperty =
        AvaloniaProperty.Register<MarkdownEditor, FontFamily>(nameof(CodeFontFamily), new FontFamily("Cascadia Mono, Consolas, Menlo, monospace"));

    /// <summary>How long after the last edit the source is scanned again.</summary>
    private static readonly TimeSpan s_scanDelay = TimeSpan.FromMilliseconds(150);

    private readonly TextEditor _editor;
    private readonly SyntaxColorizer _colorizer;
    private readonly DispatcherTimer _scanTimer;

    /// <summary>Counts the edits, so a scan of an older text is not applied.</summary>
    private int _version;

    /// <summary>Whether the text is being replaced from <see cref="Markdown"/>, so the edit is not reported back.</summary>
    private bool _settingText;

    /// <summary>Initializes a new instance of the <see cref="MarkdownEditor"/> class.</summary>
    public MarkdownEditor()
    {
        Resources.MergedDictionaries.Add(new EditorTheme());
        _editor = new TextEditor
        {
            Document = new TextDocument(),
            FontFamily = CodeFontFamily,
            ShowLineNumbers = ShowLineNumbers,
            WordWrap = WordWrap,

            // NOTE: AvaloniaEdit marks search results with a solid dark grey, which hides dark text on a light theme. A
            // translucent amber shows on both.
            SearchResultsBrush = new SolidColorBrush(Color.FromArgb(0x66, 0xF5, 0x9E, 0x0B)),
        };

        // NOTE: AvaloniaEdit opens URLs in the text on Ctrl+click, outside the scheme check of MarkdownOptions.
        _editor.Options.EnableHyperlinks = false;
        _editor.Options.EnableEmailHyperlinks = false;
        _previewSections = new PreviewReadOnlySections(this);
        ApplyPreviewReadOnlySections();

        _colorizer = new SyntaxColorizer(this);
        _editor.TextArea.TextView.LineTransformers.Add(_colorizer);
        _editor.TextArea.TextView.BackgroundRenderers.Add(new RangeMarkRenderer(_marks));
        _editor.TextArea.TextView.BackgroundRenderers.Add(new RangeMarkRenderer(_findHighlights));
        _editor.TextArea.TextView.BackgroundRenderers.Add(new RangeMarkRenderer(_previewMarks, PreviewBackgroundOpacity));
        _editor.Document.Changed += OnDocumentChanged;
        _editor.Document.UpdateFinished += OnDocumentUpdateFinished;
        _editor.TextArea.Caret.PositionChanged += (_, _) =>
        {
            ScheduleSelectionReport();
            OnSelectionMovedForFind();
        };
        _editor.TextArea.SelectionChanged += (_, _) =>
        {
            ScheduleSelectionReport();
            OnSelectionMovedForFind();
        };
        _editor.TextArea.AddHandler(KeyDownEvent, OnTextAreaKeyDown, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDownForFind, RoutingStrategies.Tunnel);
        _editor.TemplateApplied += OnEditorTemplateApplied;
        _editor.TextArea.TextView.ScrollOffsetChanged += (_, _) => OnViewChanged();
        _editor.TextArea.TextView.VisualLinesChanged += (_, _) => OnViewChanged();
        _scanTimer = new DispatcherTimer(s_scanDelay, DispatcherPriority.Background, (_, _) =>
        {
            _scanTimer!.Stop();
            _ = ScanAsync();
        });

        _toolbar = CreateToolbar();
        LogicalChildren.Add(_toolbar);
        VisualChildren.Add(_toolbar);
        LogicalChildren.Add(_editor);
        VisualChildren.Add(_editor);

        // Focus given to the editor, such as by Focus(), goes on to the text inside. Tab passes the editor itself by and
        // goes to the toolbar, then to the text.
        Focusable = true;
        IsTabStop = false;
    }

    /// <summary>Gets or sets the Markdown text. It follows every edit, and binds two-way by default.</summary>
    /// <remarks>Setting it replaces the text and clears the undo history.</remarks>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    /// <summary>Gets or sets whether the text can be read and selected but not changed.</summary>
    public bool IsReadOnly
    {
        get => GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Gets or sets whether line numbers show beside the text. The default is <see langword="true"/>.</summary>
    public bool ShowLineNumbers
    {
        get => GetValue(ShowLineNumbersProperty);
        set => SetValue(ShowLineNumbersProperty, value);
    }

    /// <summary>Gets or sets whether long lines wrap at the editor's width. The default is <see langword="true"/>.</summary>
    public bool WordWrap
    {
        get => GetValue(WordWrapProperty);
        set => SetValue(WordWrapProperty, value);
    }

    /// <summary>Gets or sets the font of the text, the same default as the viewer's code font.</summary>
    public FontFamily CodeFontFamily
    {
        get => GetValue(CodeFontFamilyProperty);
        set => SetValue(CodeFontFamilyProperty, value);
    }

    /// <summary>Gets or sets the position of the caret, as an offset in <see cref="Markdown"/>.</summary>
    public int CaretOffset
    {
        get => _editor.CaretOffset;
        set => _editor.CaretOffset = value;
    }

    /// <summary>Undoes the last edit.</summary>
    /// <returns>Whether there was an edit to undo.</returns>
    public bool Undo() => _editor.Undo();

    /// <summary>Redoes the last edit undone.</summary>
    /// <returns>Whether there was an edit to redo.</returns>
    public bool Redo() => _editor.Redo();

    /// <summary>The editor inside, for the tests and the other parts of this package.</summary>
    internal TextEditor TextEditor => _editor;

    /// <summary>The colourizer, for the tests.</summary>
    internal SyntaxColorizer Colorizer => _colorizer;

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        OnFindPropertyChanged(change);
        if (change.Property == MarkdownProperty)
        {
            SetText(Markdown ?? "");
        }
        else if (change.Property == IsReadOnlyProperty)
        {
            _editor.IsReadOnly = IsReadOnly;
            UpdateToolbarState();
        }
        else if (change.Property == ShowToolbarProperty)
        {
            _toolbar.IsVisible = ShowToolbar;
        }
        else if (change.Property == ToolbarLabelsProperty)
        {
            UpdateToolbarLabels();
        }
        else if (change.Property == ShowLineNumbersProperty)
        {
            _editor.ShowLineNumbers = ShowLineNumbers;
        }
        else if (change.Property == WordWrapProperty)
        {
            _editor.WordWrap = WordWrap;
        }
        else if (change.Property == CodeFontFamilyProperty)
        {
            _editor.FontFamily = CodeFontFamily;
        }
        else if (change.Property == SelectionBrushProperty)
        {
            ApplySelectionBrush();
        }
        else if (IsBrush(change.Property))
        {
            _editor.TextArea.TextView.Redraw();
        }
        OnPreviewPropertyChanged(change);
    }

    /// <inheritdoc/>
    protected override void OnGotFocus(FocusChangedEventArgs e)
    {
        base.OnGotFocus(e);

        // NOTE: the focus event bubbles up from the toolbar's buttons too, and those keep their focus.
        if (e.Source == this)
        {
            _editor.TextArea.Focus();
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        _toolbar.Measure(availableSize);
        var toolbar = _toolbar.DesiredSize;
        _editor.Measure(availableSize.Deflate(new Thickness(0, toolbar.Height, 0, 0)));
        MeasureFindBar();
        return new Size(Math.Max(toolbar.Width, _editor.DesiredSize.Width), toolbar.Height + _editor.DesiredSize.Height);
    }

    /// <inheritdoc/>
    protected override Size ArrangeOverride(Size finalSize)
    {
        var toolbarHeight = _toolbar.DesiredSize.Height;
        _toolbar.Arrange(new Rect(0, 0, finalSize.Width, toolbarHeight));
        _editor.Arrange(new Rect(0, toolbarHeight, finalSize.Width, Math.Max(0, finalSize.Height - toolbarHeight)));
        ArrangeFindBar(finalSize, toolbarHeight);
        ReportTextViewMoved();
        return finalSize;
    }

    /// <summary>
    /// Scans the current text on a background thread and colours the lines with the result, unless the text changed
    /// meanwhile, in which case the scan that change scheduled applies instead.
    /// </summary>
    internal async Task ScanAsync()
    {
        _scanTimer.Stop();
        var version = _version;
        var text = _editor.Document.Text;
        var spans = await Task.Run(() => MarkdownSyntaxScanner.Scan(text)).ConfigureAwait(true);
        if (version == _version)
        {
            _colorizer.SetSpans(spans);
            _editor.TextArea.TextView.Redraw();
        }
    }

    private void SetText(string text)
    {
        if (text == _editor.Document.Text)
        {
            return;
        }
        EndPreview();
        _settingText = true;
        try
        {
            _editor.Document.Text = text;
            _editor.Document.UndoStack.ClearAll();
            ClearMarks();
        }
        finally
        {
            _settingText = false;
        }
        _ = ScanAsync();
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        _version++;
        _colorizer.Shift(e.Offset, e.RemovalLength, e.InsertionLength);
        _marks.UpdateOffsets(e);
        _previewMarks.UpdateOffsets(e);
        OnDocumentChangedForPreview(e);
        if (!_editor.Document.IsInUpdate)
        {
            RemoveEmptyMarks();
            KeepFind();
            if (!_settingText && !IsResolvingPreview)
            {
                OnTextEdited();
            }
        }
    }

    /// <summary>Reports a group of changes once it ends, so a binding does not see the text halfway through it.</summary>
    private void OnDocumentUpdateFinished(object? sender, EventArgs e)
    {
        RemoveEmptyMarks();
        KeepFind();
        if (!_settingText && !IsResolvingPreview && _editor.Document.Text != Markdown)
        {
            OnTextEdited();
        }
    }

    private void OnTextEdited()
    {
        SetCurrentValue(MarkdownProperty, _editor.Document.Text);
        _scanTimer.Stop();
        _scanTimer.Start();
    }
}
