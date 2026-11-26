using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace Polhem.Markdown.Blazor;

/// <summary>
/// A reading area for a <see cref="MarkdownViewer"/>: it scrolls the viewer, keeps its text in a centred column, and
/// offers a table of contents of its headings. A narrow rail along its edge draws a short line for each heading,
/// marking the section being read. Pointing at the rail, or clicking it, slides the list of headings out over the text;
/// its pin keeps it beside the text as a column, the text centring in the room that is left.
/// </summary>
/// <remarks>
/// The viewer is the reader's child content and keeps its own parameters. The reader scrolls it, so it fills the height
/// its host gives it: give it one, as with <c>height: 100%</c> in a box of a set height.
/// </remarks>
public class MarkdownReader : ComponentBase
{
    private const string ClassAttribute = "class";
    private const string ButtonElement = "button";
    private const string TypeAttribute = "type";

    /// <summary>A pin, upright, as on the Avalonia reader's pin button.</summary>
    private const string PinIcon = "M9 2h6l-1 2v5l3 3v2h-4.25v6L12 22l-.75-2v-6H7v-2l3-3V4z";

    private MarkdownViewer? _viewer;
    private MarkdownHeading? _currentHeading;
    private bool _open;
    private bool _focusList;
    private ElementReference _rail;
    private ElementReference _firstEntry;

    /// <summary>Gets or sets the content: the <see cref="MarkdownViewer"/> the reader shows.</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    /// <summary>
    /// Gets or sets whether the table of contents is pinned beside the text as a column, rather than sliding out over
    /// it. Its pin changes it and reports the change through <see cref="OutlinePinnedChanged"/>, so it binds with
    /// <c>@bind-OutlinePinned</c>.
    /// </summary>
    [Parameter]
    public bool OutlinePinned { get; set; }

    /// <summary>Gets or sets what happens when the pin pins or unpins the table of contents.</summary>
    [Parameter]
    public EventCallback<bool> OutlinePinnedChanged { get; set; }

    /// <summary>Gets or sets the side the rail and the table of contents are on. The left by default.</summary>
    [Parameter]
    public OutlineSide OutlineSide { get; set; }

    /// <summary>
    /// Gets or sets the widest the column of text grows, in CSS pixels, centred in the room beside the rail and a pinned
    /// table of contents. 800 by default; <see langword="null"/> lets the text take all the room.
    /// </summary>
    [Parameter]
    public int? TextMaxWidth { get; set; } = 800;

    /// <summary>Gets or sets the title of the table of contents, which also names the rail. Set it to translate it.</summary>
    [Parameter]
    public string OutlineLabel { get; set; } = "Contents";

    /// <summary>Gets or sets the accessible name and tooltip of the button that pins the table of contents.</summary>
    [Parameter]
    public string PinOutlineLabel { get; set; } = "Pin";

    /// <summary>Gets or sets the accessible name and tooltip of the button that unpins the table of contents.</summary>
    [Parameter]
    public string UnpinOutlineLabel { get; set; } = "Unpin";

    /// <summary>Gets or sets further CSS classes for the outer element.</summary>
    [Parameter]
    public string? Class { get; set; }

    /// <summary>The heading of the section being read, for tests.</summary>
    internal MarkdownHeading? CurrentHeading => _currentHeading;

    private IReadOnlyList<MarkdownHeading> Headings => _viewer?.Headings ?? [];

    /// <summary>Takes the viewer the reader shows, which registers itself as it renders.</summary>
    internal void Attach(MarkdownViewer viewer)
    {
        if (ReferenceEquals(_viewer, viewer))
        {
            return;
        }
        _viewer = viewer;
        StateHasChanged();
    }

    internal void OnHeadingsChanged(MarkdownViewer viewer)
    {
        _viewer = viewer;
        if (_currentHeading is { } current && !viewer.Headings.Contains(current))
        {
            _currentHeading = null;
        }
        StateHasChanged();
    }

    /// <summary>Marks the heading of the section at <paramref name="sourceOffset"/>, which the viewer's script reports.</summary>
    internal void OnReadingChanged(MarkdownViewer viewer, int sourceOffset)
    {
        var current = viewer.Headings.LastOrDefault(heading => heading.SourceOffset <= sourceOffset);
        if (current != _currentHeading)
        {
            _currentHeading = current;
            StateHasChanged();
        }
    }

    private void ToggleFromRail()
    {
        if (OutlinePinned)
        {
            return;
        }
        _open = !_open;
        _focusList = _open;
    }

    private async Task CloseAsync(bool focusRail)
    {
        _open = false;
        if (focusRail)
        {
            await _rail.FocusAsync();
        }
    }

    private async Task OnPanelKeyDownAsync(KeyboardEventArgs e)
    {
        if (e.Key == "Escape" && !OutlinePinned)
        {
            await CloseAsync(focusRail: true);
        }
    }

    private async Task TogglePinAsync()
    {
        var pinned = !OutlinePinned;
        _open = false;
        OutlinePinned = pinned;
        await OutlinePinnedChanged.InvokeAsync(pinned);
    }

    private async Task PickHeadingAsync(MarkdownHeading heading)
    {
        _open = false;
        _currentHeading = heading;
        if (_viewer is not null)
        {
            await _viewer.ScrollToHeadingAsync(heading.Id);
        }
    }

    /// <inheritdoc/>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusList && _open)
        {
            _focusList = false;
            await _firstEntry.FocusAsync();
        }
    }

    /// <inheritdoc/>
    protected override void BuildRenderTree(RenderTreeBuilder builder)
    {
        var headings = Headings;
        var outline = headings.Count > 0;
        var classes = "pmd-reader pmd-reader-" + (OutlineSide == OutlineSide.Right ? "right" : "left")
            + (outline ? "" : " pmd-reader-plain")
            + (OutlinePinned ? " pmd-reader-pinned" : "")
            + (_open && !OutlinePinned ? " pmd-reader-open" : "")
            + (string.IsNullOrWhiteSpace(Class) ? "" : " " + Class);

        builder.OpenElement(0, "div");
        builder.AddAttribute(1, ClassAttribute, classes);
        if (TextMaxWidth is { } width)
        {
            builder.AddAttribute(2, "style", "--pmd-reader-text-width: " + width.ToString(CultureInfo.InvariantCulture) + "px");
        }
        if (outline)
        {
            builder.OpenRegion(3);
            Outline(builder, headings);
            builder.CloseRegion();
        }
        if (_open && !OutlinePinned)
        {
            // A click beside the list that a click opened lands here and closes it, as a click beside a menu does.
            builder.OpenElement(4, "div");
            builder.AddAttribute(5, ClassAttribute, "pmd-reader-backdrop");
            builder.AddAttribute(6, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => CloseAsync(focusRail: false)));
            builder.CloseElement();
        }
        builder.OpenElement(7, "div");
        builder.AddAttribute(8, ClassAttribute, "pmd-reader-scroll");
        builder.OpenElement(9, "div");
        builder.AddAttribute(10, ClassAttribute, "pmd-reader-text");
        builder.OpenComponent<CascadingValue<MarkdownReader>>(11);
        builder.AddComponentParameter(12, "Value", this);
        builder.AddComponentParameter(13, "IsFixed", true);
        builder.AddComponentParameter(14, "ChildContent", ChildContent);
        builder.CloseComponent();
        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    }

    /// <summary>
    /// Writes the rail and the list in a navigation landmark. The stylesheet shows the list while the pointer is on the
    /// landmark, while the list has the focus, when a click opened it, or pinned.
    /// </summary>
    private void Outline(RenderTreeBuilder builder, IReadOnlyList<MarkdownHeading> headings)
    {
        builder.OpenElement(0, "nav");
        builder.AddAttribute(1, ClassAttribute, "pmd-reader-outline");
        builder.AddAttribute(2, "aria-label", OutlineLabel);

        builder.OpenElement(3, ButtonElement);
        builder.AddAttribute(4, TypeAttribute, ButtonElement);
        builder.AddAttribute(5, ClassAttribute, "pmd-reader-rail");
        builder.AddAttribute(6, "aria-label", OutlineLabel);
        builder.AddAttribute(7, "title", OutlineLabel);
        builder.AddAttribute(8, "aria-expanded", OutlinePinned || _open ? "true" : "false");
        builder.AddAttribute(9, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, ToggleFromRail));
        builder.AddElementReferenceCapture(10, element => _rail = element);
        foreach (var heading in headings)
        {
            builder.OpenElement(11, "span");
            builder.AddAttribute(12, ClassAttribute, "pmd-reader-mark pmd-reader-mark-" + Level(heading)
                + (heading == _currentHeading ? " pmd-reader-current" : ""));
            builder.CloseElement();
        }
        builder.CloseElement();

        builder.OpenElement(13, "div");
        builder.AddAttribute(14, ClassAttribute, "pmd-reader-panel");
        builder.AddAttribute(15, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(this, OnPanelKeyDownAsync));
        builder.OpenRegion(16);
        Header(builder);
        builder.CloseRegion();
        builder.OpenElement(17, "ul");
        for (var index = 0; index < headings.Count; index++)
        {
            builder.OpenRegion(18);
            Entry(builder, headings[index], first: index == 0);
            builder.CloseRegion();
        }
        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    }

    private void Header(RenderTreeBuilder builder)
    {
        var label = OutlinePinned ? UnpinOutlineLabel : PinOutlineLabel;
        builder.OpenElement(0, "div");
        builder.AddAttribute(1, ClassAttribute, "pmd-reader-header");
        builder.OpenElement(2, "span");
        builder.AddContent(3, OutlineLabel);
        builder.CloseElement();
        builder.OpenElement(4, ButtonElement);
        builder.AddAttribute(5, TypeAttribute, ButtonElement);
        builder.AddAttribute(6, ClassAttribute, "pmd-reader-pin");
        builder.AddAttribute(7, "aria-label", label);
        builder.AddAttribute(8, "title", label);
        builder.AddAttribute(9, "aria-pressed", OutlinePinned ? "true" : "false");
        builder.AddAttribute(10, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, TogglePinAsync));
        builder.OpenElement(11, "svg");
        builder.AddAttribute(12, "viewBox", "0 0 24 24");
        builder.AddAttribute(13, "aria-hidden", "true");
        builder.OpenElement(14, "path");
        builder.AddAttribute(15, "d", PinIcon);
        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
        builder.CloseElement();
    }

    private void Entry(RenderTreeBuilder builder, MarkdownHeading heading, bool first)
    {
        var current = heading == _currentHeading;
        builder.OpenElement(0, "li");
        builder.OpenElement(1, "a");
        builder.SetKey(heading.Id);
        builder.AddAttribute(2, ClassAttribute, "pmd-reader-entry pmd-reader-level-" + Level(heading) + (current ? " pmd-reader-current" : ""));
        builder.AddAttribute(3, "href", _viewer?.AnchorHref(heading.Id) ?? "#" + heading.Id);
        if (current)
        {
            builder.AddAttribute(4, "aria-current", "location");
        }
        builder.AddAttribute(5, "onclick", EventCallback.Factory.Create<MouseEventArgs>(this, () => PickHeadingAsync(heading)));
        builder.AddEventPreventDefaultAttribute(6, "onclick", true);
        if (first)
        {
            builder.AddElementReferenceCapture(7, element => _firstEntry = element);
        }
        builder.AddContent(8, heading.Text);
        builder.CloseElement();
        builder.CloseElement();
    }

    private static string Level(MarkdownHeading heading) =>
        Math.Clamp(heading.Level, 1, 6).ToString(CultureInfo.InvariantCulture);
}
