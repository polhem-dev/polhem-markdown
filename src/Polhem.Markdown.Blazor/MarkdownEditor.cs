using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.JSInterop;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Blazor;

/// <summary>
/// Edits a Markdown text as source, colored the way <see cref="MarkdownViewer"/> reads it: markers, headings, emphasis,
/// code, links, raw HTML and formulas, and the code of fenced blocks with the viewer's highlighting.
/// </summary>
/// <remarks>
/// <para>
/// The text is typed into a native textarea, so input methods, on-screen keyboards and screen readers work as they do
/// anywhere on the page, and the colors are drawn behind it by the package's <c>polhem-markdown-editor.js</c> module,
/// which the component loads itself. Styles are in <c>_content/Polhem.Markdown.Blazor/polhem-markdown.css</c>, the
/// stylesheet of the viewer; the <c>--pmd-*</c> CSS variables listed in the package README set the colors and the font.
/// The editor fills the height its host gives it.
/// </para>
/// <para>
/// <see cref="Markdown"/> follows every edit through <see cref="MarkdownChanged"/>, so <c>@bind-Markdown</c> keeps a
/// field up to date. The colors follow a moment after typing stops; until then they move with the edits. The editor
/// keeps its own undo history, because the browsers' own does not survive the edits the editor makes itself.
/// </para>
/// <para>
/// It works under interactive server, WebAssembly and auto rendering. Rendered statically, it shows the text read-only.
/// </para>
/// </remarks>
public class MarkdownEditor : ComponentBase, IAsyncDisposable
{
    private const string ModulePath = "./_content/Polhem.Markdown.Blazor/polhem-markdown-editor.js";

    /// <summary>How long after the last edit a text is scanned again, before the time of the last scan is added.</summary>
    private const int ScanDelayMilliseconds = 150;

    /// <summary>
    /// The most the wait before a scan grows. Under WebAssembly the scan runs on the page's only thread, so a large text,
    /// whose scan holds the page longer, waits longer for typing to stop.
    /// </summary>
    private const int MaxScanDelayMilliseconds = 1000;

    private static readonly string[] s_kindClasses = SyntaxSpanPatch.KindClasses();

    /// <summary>The value of the textarea in the first render. Later changes reach it through the module.</summary>
    private string? _initialText;

    /// <summary>The text as the textarea holds it, with <c>\n</c> line breaks, kept up to date by the module.</summary>
    private string _text = "";

    /// <summary>The text as last handed to the host, or as the host last set it.</summary>
    private string? _reported;

    /// <summary>Texts handed to the host whose handlers have not finished, which may still come back as parameters.</summary>
    private readonly List<string> _reporting = [];

    private bool _crLf;

    /// <summary>The number of the last edit the module sent, so the colors of a scan are matched to the text scanned.</summary>
    private int _version;

    /// <summary>The spans the module has, and the length of the text they are for, to send the next scan as a patch.</summary>
    private int[] _sentSpans = [];
    private int _sentLength;

    private double _lastScanMilliseconds;
    private CancellationTokenSource? _scan;
    private Task _scanTask = Task.CompletedTask;

    /// <summary>Whether the module reports scrolling, as it was last told.</summary>
    private bool _reportingScroll;
    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<MarkdownEditorBridge>? _bridge;
    private bool _disposed;

    /// <summary>Gets or sets the Markdown text.</summary>
    /// <remarks>
    /// Setting it to a text other than the one the editor holds replaces the text and clears the undo history. Line
    /// breaks come back as the text set used them: <c>\r\n</c> if it had any, otherwise <c>\n</c>.
    /// </remarks>
    [Parameter]
    public string? Markdown { get; set; }

    /// <summary>Gets or sets what happens when an edit changes the text: it receives the new text.</summary>
    [Parameter]
    public EventCallback<string> MarkdownChanged { get; set; }

    /// <summary>
    /// Gets or sets what happens when scrolling brings another line to the top of the editor: it receives
    /// <see cref="FirstVisibleSourceOffset"/>.
    /// </summary>
    /// <remarks>
    /// The offset follows at most once a frame while the editor scrolls, including when typing or
    /// <see cref="ScrollToSourceOffsetAsync"/> scrolls it.
    /// </remarks>
    [Parameter]
    public EventCallback<int> FirstVisibleSourceOffsetChanged { get; set; }

    /// <summary>
    /// Gets the offset in <see cref="Markdown"/> of the text at the top of the editor, as last reported while
    /// <see cref="FirstVisibleSourceOffsetChanged"/> is set; 0 before that.
    /// </summary>
    /// <remarks>
    /// The offset is in the host's text, with <c>\r\n</c> line breaks counted as two when the text uses them, so it can
    /// be given to <see cref="MarkdownViewer.ScrollToSourceOffsetAsync"/> as it is.
    /// </remarks>
    public int FirstVisibleSourceOffset { get; private set; }

    /// <summary>Gets or sets whether the text can be read and selected but not changed.</summary>
    [Parameter]
    public bool IsReadOnly { get; set; }

    /// <summary>Gets or sets whether long lines wrap at the editor's width. The default is <see langword="true"/>.</summary>
    [Parameter]
    public bool WordWrap { get; set; } = true;

    /// <summary>Gets or sets further CSS classes for the outer element.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>
    /// Gets or sets further attributes for the textarea, such as <c>aria-label</c> or <c>id</c> to give it an accessible
    /// name.
    /// </summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public IReadOnlyDictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    /// <summary>The text as the editor holds it, with <c>\n</c> line breaks, for the tests.</summary>
    internal string Text => _text;

    /// <summary>The scan scheduled last, for the tests to wait for: sending its spans renders nothing.</summary>
    internal Task PendingScan => _scanTask;

    /// <summary>
    /// Puts the caret at <paramref name="sourceOffset"/> in <see cref="Markdown"/>, with nothing selected. It does not
    /// scroll; <see cref="ScrollToSourceOffsetAsync"/> does.
    /// </summary>
    /// <param name="sourceOffset">An offset in <see cref="Markdown"/>, in UTF-16 code units.</param>
    /// <returns>A task that completes when the request has reached the page.</returns>
    public async Task SetCaretOffsetAsync(int sourceOffset)
    {
        if (_module is not null)
        {
            await CallModuleAsync("setCaret", _root, EditorLineEndings.ToTextareaOffset(_text, sourceOffset, _crLf));
        }
    }

    /// <summary>Moves the keyboard focus to the text, without scrolling it.</summary>
    /// <returns>A task that completes when the focus has moved.</returns>
    public async Task FocusAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("focus", _root);
        }
    }

    /// <summary>Undoes the last edit.</summary>
    /// <returns>Whether there was an edit to undo.</returns>
    public async Task<bool> UndoAsync() => _module is not null && await _module.InvokeAsync<bool>("undo", _root);

    /// <summary>Redoes the last edit undone.</summary>
    /// <returns>Whether there was an edit to redo.</returns>
    public async Task<bool> RedoAsync() => _module is not null && await _module.InvokeAsync<bool>("redo", _root);

    /// <summary>
    /// Scrolls the editor so that the line holding <paramref name="sourceOffset"/> in <see cref="Markdown"/> is at its
    /// top, moved down in proportion to how far into a wrapped line the offset is.
    /// </summary>
    /// <param name="sourceOffset">
    /// An offset in <see cref="Markdown"/>, in UTF-16 code units, such as
    /// <see cref="MarkdownViewer.FirstVisibleSourceOffset"/>.
    /// </param>
    /// <returns>A task that completes when the request has reached the page.</returns>
    public async Task ScrollToSourceOffsetAsync(int sourceOffset)
    {
        if (_module is not null)
        {
            var offset = EditorLineEndings.ToTextareaOffset(_text, sourceOffset, _crLf);
            await CallModuleAsync("scrollToSource", _root, offset);
        }
    }

    /// <summary>
    /// Takes the offset of the text now at the top of the editor, from the module, as an offset in the textarea's text.
    /// Under interactive server rendering the value comes from the browser, so it is kept within the text.
    /// </summary>
    internal async Task OnFirstVisibleChangedAsync(int textareaOffset)
    {
        var offset = EditorLineEndings.ToHostOffset(_text, textareaOffset, _crLf);
        if (offset == FirstVisibleSourceOffset)
        {
            return;
        }
        FirstVisibleSourceOffset = offset;
        await FirstVisibleSourceOffsetChanged.InvokeAsync(offset);
    }

    /// <summary>
    /// Applies <paramref name="format"/> to the selection, or removes it when the selection already has it, as the
    /// shortcuts do.
    /// </summary>
    /// <remarks>
    /// An inline format wraps the selection in its markers, or inserts a pair with the caret between them when nothing
    /// is selected; a line format applies to every line the selection touches. The change is one step to undo. The
    /// shortcuts are Ctrl (Command on a Mac) with B for bold, I for italic, K for a link and E for inline code.
    /// </remarks>
    /// <param name="format">The format to apply or remove.</param>
    /// <returns>A task that completes when the request has reached the page; the edit follows from there.</returns>
    public async Task ApplyFormatAsync(MarkdownFormat format)
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("command", _root, format.ToString());
        }
    }

    /// <summary>
    /// The edit of a command the module asks for, for the text as it was after edit <paramref name="version"/>:
    /// <c>format</c> with a <see cref="MarkdownFormat"/> name as <paramref name="argument"/>, <c>continueList</c>,
    /// <c>indent</c> or <c>outdent</c>. It is the start, length and text of the change and the selection after it, or
    /// <see langword="null"/> when there is nothing to do or the text has changed since, in which case the module
    /// drops the command.
    /// </summary>
    /// <remarks>
    /// Under interactive server rendering the values come from the browser; the formatter keeps the selection within
    /// the text, and an unknown command or format does nothing.
    /// </remarks>
    internal object?[]? Command(string command, int version, int start, int length, string? argument)
    {
        if (version != _version)
        {
            return null;
        }
        var edit = command switch
        {
            "format" when Enum.TryParse<MarkdownFormat>(argument, out var format) && Enum.IsDefined(format) =>
                MarkdownFormatter.Apply(_text, start, length, format),
            "continueList" => MarkdownFormatter.ContinueList(_text, start),
            "indent" => MarkdownFormatter.IndentListItem(_text, start, length, outdent: false),
            "outdent" => MarkdownFormatter.IndentListItem(_text, start, length, outdent: true),
            _ => (MarkdownEdit?)null,
        };
        return edit is { } change
            ? [change.Start, change.Length, change.Text, change.SelectionStart, change.SelectionLength]
            : null;
    }

    /// <inheritdoc/>
    protected override async Task OnParametersSetAsync()
    {
        if (_module is not null && FirstVisibleSourceOffsetChanged.HasDelegate != _reportingScroll)
        {
            _reportingScroll = FirstVisibleSourceOffsetChanged.HasDelegate;
            await CallModuleAsync("watchScroll", _root, _reportingScroll);
        }

        var markdown = Markdown ?? "";
        if (string.Equals(markdown, _reported, StringComparison.Ordinal)
            || _reporting.Contains(markdown, StringComparer.Ordinal))
        {
            // The text the editor holds, or an earlier one of its own coming back while a later edit is on its way.
            return;
        }

        _reported = markdown;
        _crLf = EditorLineEndings.UsesCrLf(markdown);
        var text = EditorLineEndings.ToTextarea(markdown);
        if (string.Equals(text, _text, StringComparison.Ordinal))
        {
            return;
        }
        _text = text;
        _initialText ??= text;
        if (_module is not null)
        {
            await CallModuleAsync("setText", _root, text);
            ScheduleScan(immediately: true);
        }
    }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        _initialText ??= _text;
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, "class", CombineClasses("pmd-editor", WordWrap ? null : "pmd-editor-nowrap", Class));
        builder.AddElementReferenceCapture(2, element => _root = element);

        // The layer the module draws the colors on. Blazor renders it empty and never touches its content.
        builder.OpenElement(3, "pre");
        builder.AddAttribute(4, "class", "pmd-editor-layer");
        builder.AddAttribute(5, "aria-hidden", "true");
        builder.CloseElement();

        builder.OpenElement(6, "textarea");
        builder.AddMultipleAttributes(7, AdditionalAttributes);
        builder.AddAttribute(8, "class", "pmd-editor-input");
        builder.AddAttribute(9, "spellcheck", "false");
        builder.AddAttribute(10, "wrap", WordWrap ? "soft" : "off");
        builder.AddAttribute(11, "readonly", IsReadOnly || !RendererInfo.IsInteractive);

        // NOTE: the value is the same in every render, so Blazor sets it once. After that the textarea is the user's,
        // and the module changes it when the host sets another text: writing it from here while the user types would
        // throw away what they typed since the last round trip.
        builder.AddAttribute(12, "value", _initialText);
        builder.CloseElement();

        builder.CloseElement();
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || !RendererInfo.IsInteractive)
        {
            return;
        }
        _bridge = DotNetObjectReference.Create(new MarkdownEditorBridge(this));
        try
        {
            _module = await JS.InvokeAsync<IJSObjectReference>("import", ModulePath);
            _sentSpans = [];
            _sentLength = 0;
            _reportingScroll = FirstVisibleSourceOffsetChanged.HasDelegate;
            await _module.InvokeVoidAsync("init", _root, _bridge, _text, s_kindClasses, _reportingScroll);
        }
        catch (JSException)
        {
            // The module could not load. The textarea still edits the text, without colors, and its edits stay in the
            // page.
            _module = null;
            return;
        }
        catch (JSDisconnectedException)
        {
            return;
        }
        ScheduleScan(immediately: true);
    }

    /// <summary>
    /// Takes an edit of the textarea, sent by the module: <paramref name="removed"/> characters at
    /// <paramref name="start"/> replaced by <paramref name="text"/>. A large edit arrives in parts, all but the last
    /// with <paramref name="more"/> set.
    /// </summary>
    /// <remarks>
    /// Under interactive server rendering this is called from the browser, whose values cannot be trusted; a part that
    /// does not fit the text is refused.
    /// </remarks>
    internal async Task ApplyEditAsync(int version, int start, int removed, string text, bool more)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (start < 0 || removed < 0 || start > _text.Length || removed > _text.Length - start || version < _version)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "The edit does not fit the text.");
        }
        _text = string.Concat(_text.AsSpan(0, start), text, _text.AsSpan(start + removed));
        if (more)
        {
            return;
        }

        _version = version;
        ScheduleScan(immediately: false);
        var reported = EditorLineEndings.ToHost(_text, _crLf);
        _reported = reported;
        _reporting.Add(reported);
        try
        {
            await MarkdownChanged.InvokeAsync(reported);
        }
        finally
        {
            _reporting.Remove(reported);
        }
    }

    private void ScheduleScan(bool immediately)
    {
        _scan?.Cancel();
        _scan?.Dispose();
        _scan = new CancellationTokenSource();
        var delay = immediately
            ? 0
            : Math.Min(MaxScanDelayMilliseconds, ScanDelayMilliseconds + (int)(2 * _lastScanMilliseconds));
        _scanTask = ScanAsync(delay, _scan.Token);
    }

    /// <summary>
    /// Scans the text after <paramref name="delay"/> milliseconds and sends the module the spans that changed, unless
    /// another edit came meanwhile; the scan that edit scheduled sends them instead.
    /// </summary>
    private async Task ScanAsync(int delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        var text = _text;
        var version = _version;
        var watch = Stopwatch.StartNew();
        var spans = SyntaxSpanPatch.Pack(await Task.Run(() => MarkdownSyntaxScanner.Scan(text), CancellationToken.None));
        _lastScanMilliseconds = watch.Elapsed.TotalMilliseconds;
        if (token.IsCancellationRequested || _module is null || _disposed)
        {
            return;
        }

        var patch = SyntaxSpanPatch.Diff(_sentSpans, _sentLength, spans, text.Length);
        _sentSpans = spans;
        _sentLength = text.Length;
        await CallModuleAsync("applySpans", _root, version, MemoryMarshal.AsBytes(patch.AsSpan()).ToArray());
    }

    /// <summary>
    /// Calls the module where nothing waits for the answer. A failure leaves the colors behind until the next scan, and
    /// must not end the circuit.
    /// </summary>
    private async Task CallModuleAsync(string identifier, params object?[] args)
    {
        if (_module is null)
        {
            return;
        }
        try
        {
            await _module.InvokeVoidAsync(identifier, args);
        }
        catch (JSException)
        {
            // The editor left the page between the edit and the call.
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone.
        }
        catch (TaskCanceledException)
        {
            // The call timed out on a slow connection.
        }
    }

    private static string CombineClasses(params string?[] classes) =>
        string.Join(' ', classes.Where(name => !string.IsNullOrWhiteSpace(name)));

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore();
        GC.SuppressFinalize(this);
    }

    /// <summary>Stops the pending scan and releases the script module and the editor's state in the page.</summary>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        _disposed = true;
        if (_scan is not null)
        {
            await _scan.CancelAsync();
            _scan.Dispose();
            _scan = null;
        }
        var module = _module;
        _module = null;
        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", _root);
                await module.DisposeAsync();
            }
            catch (JSException)
            {
                // The editor already left the page.
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already gone, and the module with it.
            }
            catch (TaskCanceledException)
            {
                // The page did not answer in time; it is going away.
            }
        }
        _bridge?.Dispose();
        _bridge = null;
    }
}
