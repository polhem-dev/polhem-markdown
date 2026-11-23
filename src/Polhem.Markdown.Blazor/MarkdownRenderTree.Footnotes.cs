using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

internal sealed partial class MarkdownRenderTree
{
    /// <summary>
    /// Writes the footnotes as a numbered list in a section of their own, which the stylesheet sets off with a rule. Each
    /// note is keyed and captured by its id, as headings are, and takes focus from script only.
    /// </summary>
    private void Footnotes(FootnotesNode footnotes)
    {
        _builder.OpenElement(0, "section");
        _builder.AddAttribute(1, ClassAttribute, "pmd-footnotes");
        _builder.OpenElement(2, "ol");
        foreach (var note in footnotes.Notes)
        {
            var id = note.Id;
            _builder.OpenRegion(3);
            _builder.OpenElement(0, "li");
            _builder.SetKey(id);
            _builder.AddAttribute(1, "tabindex", "-1");
            _builder.AddElementReferenceCapture(2, element => _viewer.SetAnchorElement(id, element));
            Blocks(3, note.Blocks);
            _builder.CloseElement();
            _builder.CloseRegion();
            _rendered.Add(typeof(FootnoteNode));
        }
        _builder.CloseElement();
        _builder.CloseElement();
    }

    /// <summary>
    /// Writes a reference to a footnote as a superscript link to the note. The link itself is what a back link returns
    /// to, so it is keyed and captured by the reference's id.
    /// </summary>
    private void FootnoteReference(FootnoteReferenceNode reference)
    {
        var id = reference.Id;
        var noteId = reference.NoteId;
        _builder.OpenElement(0, "sup");
        _builder.AddAttribute(1, ClassAttribute, "pmd-fnref");
        _builder.OpenElement(2, "a");
        _builder.SetKey(id);
        _builder.AddAttribute(3, "href", _viewer.AnchorHref(noteId));
        _builder.AddAttribute(4, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.MoveToAnchorAsync(noteId)));
        _builder.AddEventPreventDefaultAttribute(5, OnClickAttribute, true);
        _builder.AddElementReferenceCapture(6, element => _viewer.SetAnchorElement(id, element));
        _builder.AddContent(7, reference.Number.ToString(CultureInfo.InvariantCulture));
        _builder.CloseElement();
        _builder.CloseElement();
    }

    /// <summary>Writes a back link from a footnote to one reference to it, after a space, as GitHub does.</summary>
    private void FootnoteBackLink(FootnoteBackLinkNode back)
    {
        var referenceId = back.ReferenceId;
        var position = back.Position > 1 ? back.Position.ToString(CultureInfo.InvariantCulture) : "";
        _builder.AddContent(0, " ");
        _builder.OpenElement(1, "a");
        _builder.AddAttribute(2, ClassAttribute, "pmd-fnback");
        _builder.AddAttribute(3, "href", _viewer.AnchorHref(referenceId));
        _builder.AddAttribute(4, "aria-label", position.Length == 0 ? "Back to reference" : "Back to reference " + position);
        _builder.AddAttribute(5, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.MoveToAnchorAsync(referenceId)));
        _builder.AddEventPreventDefaultAttribute(6, OnClickAttribute, true);
        _builder.AddContent(7, "\u21A9" + position);
        _builder.CloseElement();
    }
}
