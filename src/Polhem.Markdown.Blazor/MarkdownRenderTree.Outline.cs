using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Polhem.Markdown.Blazor;

internal sealed partial class MarkdownRenderTree
{
    /// <summary>A pin, upright, as on the Avalonia viewer's pin button.</summary>
    private const string PinIcon = "M9 2h6l-1 2v5l3 3v2h-4.25v6L12 22l-.75-2v-6H7v-2l3-3V4z";

    private const string OutlineIcon = "M3 5h2v2H3zm4 0h14v2H7zM3 11h2v2H3zm4 0h14v2H7zm-4 6h2v2H3zm4 0h14v2H7z";

    /// <summary>
    /// Writes the table of contents: a navigation landmark with the button that opens it, or, pinned or open, the list of
    /// headings. The stylesheet keeps it in view as the text scrolls.
    /// </summary>
    public void Outline(IReadOnlyList<MarkdownHeading> headings)
    {
        var pinned = _viewer.OutlinePinned;
        _builder.OpenElement(0, "nav");
        _builder.AddAttribute(1, ClassAttribute, "pmd-outline");
        _builder.AddAttribute(2, "aria-label", _viewer.OutlineLabel);
        if (!pinned)
        {
            _builder.OpenElement(3, "button");
            _builder.AddAttribute(4, "type", "button");
            _builder.AddAttribute(5, ClassAttribute, "pmd-outline-toggle");
            _builder.AddAttribute(6, "aria-label", _viewer.OutlineLabel);
            _builder.AddAttribute(7, "title", _viewer.OutlineLabel);
            _builder.AddAttribute(8, "aria-expanded", _viewer.OutlineOpen ? "true" : "false");
            _builder.AddAttribute(9, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, _viewer.ToggleOutline));
            _builder.AddElementReferenceCapture(10, _viewer.SetOutlineButton);
            Icon(11, OutlineIcon);
            _builder.CloseElement();
        }
        if (pinned || _viewer.OutlineOpen)
        {
            if (!pinned)
            {
                // A click beside the floating list lands here and closes it, as a click beside a menu does.
                _builder.OpenElement(12, "div");
                _builder.AddAttribute(13, ClassAttribute, "pmd-outline-backdrop");
                _builder.AddAttribute(14, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.CloseOutline(focusButton: false)));
                _builder.CloseElement();
            }
            _builder.OpenElement(15, "div");
            _builder.AddAttribute(16, ClassAttribute, "pmd-outline-panel");
            _builder.AddAttribute(17, "onkeydown", EventCallback.Factory.Create<KeyboardEventArgs>(_viewer, _viewer.OnOutlineKeyDown));
            OutlineHeader(18, pinned);
            _builder.OpenElement(19, "ul");
            for (var index = 0; index < headings.Count; index++)
            {
                _builder.OpenRegion(20);
                OutlineEntry(headings[index], first: index == 0);
                _builder.CloseRegion();
            }
            _builder.CloseElement();
            _builder.CloseElement();
        }
        _builder.CloseElement();
    }

    private void OutlineHeader(int sequence, bool pinned)
    {
        _builder.OpenRegion(sequence);
        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, "pmd-outline-header");
        _builder.OpenElement(2, "span");
        _builder.AddContent(3, _viewer.OutlineLabel);
        _builder.CloseElement();
        var label = pinned ? _viewer.UnpinOutlineLabel : _viewer.PinOutlineLabel;
        _builder.OpenElement(4, "button");
        _builder.AddAttribute(5, "type", "button");
        _builder.AddAttribute(6, ClassAttribute, "pmd-outline-pin");
        _builder.AddAttribute(7, "aria-label", label);
        _builder.AddAttribute(8, "title", label);
        _builder.AddAttribute(9, "aria-pressed", pinned ? "true" : "false");
        _builder.AddAttribute(10, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, _viewer.TogglePinAsync));
        Icon(11, PinIcon);
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseRegion();
    }

    private void OutlineEntry(MarkdownHeading heading, bool first)
    {
        var current = heading == _viewer.CurrentHeading;
        _builder.OpenElement(0, "li");
        _builder.OpenElement(1, "a");
        _builder.SetKey(heading.Id);
        _builder.AddAttribute(2, ClassAttribute, "pmd-outline-entry pmd-outline-level-"
            + Math.Clamp(heading.Level, 1, 6).ToString(CultureInfo.InvariantCulture) + (current ? " pmd-outline-current" : ""));
        _builder.AddAttribute(3, "href", _viewer.AnchorHref(heading.Id));
        if (current)
        {
            _builder.AddAttribute(4, "aria-current", "location");
        }
        _builder.AddAttribute(5, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.PickHeadingAsync(heading)));
        _builder.AddEventPreventDefaultAttribute(6, OnClickAttribute, true);
        if (first)
        {
            _builder.AddElementReferenceCapture(7, _viewer.SetOutlineFirstEntry);
        }
        _builder.AddContent(8, heading.Text);
        _builder.CloseElement();
        _builder.CloseElement();
    }

    private void Icon(int sequence, string path)
    {
        _builder.OpenRegion(sequence);
        _builder.OpenElement(0, "svg");
        _builder.AddAttribute(1, "viewBox", "0 0 24 24");
        _builder.AddAttribute(2, "aria-hidden", "true");
        _builder.OpenElement(3, "path");
        _builder.AddAttribute(4, "d", path);
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseRegion();
    }
}
