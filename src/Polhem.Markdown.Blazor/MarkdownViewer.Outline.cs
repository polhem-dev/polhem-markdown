using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Polhem.Markdown.Blazor;

public partial class MarkdownViewer
{
    private bool _outlineOpen;
    private bool _focusOutline;
    private bool _focusOutlineButton;
    private MarkdownHeading? _currentHeading;
    private ElementReference _outlineButton;
    private ElementReference _outlineFirstEntry;

    /// <summary>
    /// Gets or sets whether the viewer offers a table of contents of the text's headings: a button in its top corner that
    /// opens the list floating over the text, which <see cref="OutlinePinned"/> pins beside it. Off by default. The
    /// button and the list stay in view as the page or the element around the viewer scrolls, and the section being
    /// read is marked.
    /// </summary>
    [Parameter]
    public bool ShowOutline { get; set; }

    /// <summary>
    /// Gets or sets whether the table of contents is pinned as a column beside the text, rather than opened from a
    /// button. The pin button in the list changes it and reports the change through <see cref="OutlinePinnedChanged"/>,
    /// so it binds with <c>@bind-OutlinePinned</c>.
    /// </summary>
    [Parameter]
    public bool OutlinePinned { get; set; }

    /// <summary>Gets or sets what happens when the pin button pins or unpins the table of contents.</summary>
    [Parameter]
    public EventCallback<bool> OutlinePinnedChanged { get; set; }

    /// <summary>Gets or sets the side the table of contents is on. The left by default.</summary>
    [Parameter]
    public OutlineSide OutlineSide { get; set; }

    /// <summary>Gets or sets the title of the table of contents, which also names its button. Set it to translate it.</summary>
    [Parameter]
    public string OutlineLabel { get; set; } = "Contents";

    /// <summary>Gets or sets the accessible name and tooltip of the button that pins the table of contents.</summary>
    [Parameter]
    public string PinOutlineLabel { get; set; } = "Pin";

    /// <summary>Gets or sets the accessible name and tooltip of the button that unpins the table of contents.</summary>
    [Parameter]
    public string UnpinOutlineLabel { get; set; } = "Unpin";

    /// <summary>Whether the table of contents is shown: it is on, and the text has headings.</summary>
    internal bool OutlineShown => ShowOutline && Headings.Count > 0;

    /// <summary>Whether the floating table of contents is open.</summary>
    internal bool OutlineOpen => _outlineOpen && !OutlinePinned && OutlineShown;

    /// <summary>The heading of the section being read.</summary>
    internal MarkdownHeading? CurrentHeading => _currentHeading;

    internal void SetOutlineButton(ElementReference element) => _outlineButton = element;

    internal void SetOutlineFirstEntry(ElementReference element) => _outlineFirstEntry = element;

    internal void ToggleOutline()
    {
        _outlineOpen = !_outlineOpen;
        _focusOutline = _outlineOpen;
    }

    internal void CloseOutline(bool focusButton)
    {
        if (!OutlineOpen)
        {
            return;
        }
        _outlineOpen = false;
        _focusOutlineButton = focusButton;
    }

    internal void OnOutlineKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Escape")
        {
            CloseOutline(focusButton: true);
        }
    }

    internal async Task TogglePinAsync()
    {
        var pinned = !OutlinePinned;
        _outlineOpen = false;
        OutlinePinned = pinned;
        await OutlinePinnedChanged.InvokeAsync(pinned);
    }

    internal async Task PickHeadingAsync(MarkdownHeading heading)
    {
        _outlineOpen = false;
        _currentHeading = heading;
        await ScrollToHeadingAsync(heading.Id);
    }

    /// <summary>Marks the heading of the section at <paramref name="sourceOffset"/>, which the script reports.</summary>
    internal void OnReadingChanged(int sourceOffset)
    {
        var current = Headings.LastOrDefault(heading => heading.SourceOffset <= sourceOffset);
        if (current != _currentHeading)
        {
            _currentHeading = current;
            StateHasChanged();
        }
    }

    /// <summary>Forgets a marked heading the text no longer has.</summary>
    private void KeepCurrentHeading()
    {
        if (_currentHeading is { } current && !Headings.Contains(current))
        {
            _currentHeading = null;
        }
    }

    /// <summary>Moves the focus into the list once it has opened, or back to its button once it has closed.</summary>
    private async Task FocusOutlineAsync()
    {
        if (_focusOutline && OutlineOpen)
        {
            _focusOutline = false;
            await _outlineFirstEntry.FocusAsync();
        }
        else if (_focusOutlineButton)
        {
            _focusOutlineButton = false;
            await _outlineButton.FocusAsync();
        }
    }
}
