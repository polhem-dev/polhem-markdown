using System.Globalization;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Output;

internal sealed partial class HtmlWriter
{
    private void Inlines(IReadOnlyList<InlineNode> inlines)
    {
        foreach (var inline in inlines)
        {
            Inline(inline);
        }
    }

    private void Inline(InlineNode inline)
    {
        var written = true;
        switch (inline)
        {
            case TextNode text:
                Text(text.Text);
                break;
            case CodeSpanNode code:
                _html.Append("<code>");
                Text(code.Code);
                _html.Append("</code>");
                break;
            case EmphasisNode emphasis:
                Element(emphasis.Kind switch
                {
                    EmphasisKind.Bold => "strong",
                    EmphasisKind.Strikethrough => "del",
                    EmphasisKind.Superscript => "sup",
                    EmphasisKind.Subscript => "sub",
                    EmphasisKind.Keyboard => "kbd",
                    _ => "em",
                }, emphasis.Inlines);
                break;
            case LinkNode link:
                // As in the viewer when the host does not handle links: the page opens in a new one, which learns nothing
                // of this one.
                _html.Append("<a");
                Attribute("href", link.Target.AbsoluteUri);
                _html.Append(" target=\"_blank\" rel=\"noopener noreferrer\">");
                Inlines(link.Inlines);
                _html.Append("</a>");
                break;
            case AnchorLinkNode anchor:
                _html.Append("<a");
                Attribute("href", "#" + anchor.Id);
                _html.Append('>');
                Inlines(anchor.Inlines);
                _html.Append("</a>");
                break;
            case LineBreakNode:
                _html.Append("<br>");
                break;
            case ImageNode image:
                Image(image);
                break;
            case MathNode math:
                Formula(math.Formula, math.Source, display: false);
                break;
            case FootnoteReferenceNode reference:
                _html.Append("<sup class=\"pmd-fnref\"><a");
                Attribute("id", reference.Id);
                Attribute("href", "#" + reference.NoteId);
                _html.Append('>').Append(reference.Number.ToString(CultureInfo.InvariantCulture)).Append("</a></sup>");
                break;
            case FootnoteBackLinkNode back:
                _html.Append(" <a class=\"pmd-fnback\"");
                Attribute("href", "#" + back.ReferenceId);
                Attribute("aria-label", ViewerMarkup.BackLinkLabel(back));
                _html.Append('>');
                Text(ViewerMarkup.BackLinkText(back));
                _html.Append("</a>");
                break;
            default:
                written = false;
                break;
        }
        if (written)
        {
            _written.Add(inline.GetType());
        }
    }
}
