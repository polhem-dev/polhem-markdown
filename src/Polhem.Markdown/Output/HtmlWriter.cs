using System.Globalization;
using System.Net;
using System.Text;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Output;

/// <summary>
/// Writes the model as HTML text, for <see cref="MarkdownHtml"/>. The elements, attributes and classes follow the Blazor
/// viewer's render tree (MarkdownRenderTree in that package), without the parts a reader interacts with, so that the
/// viewer's stylesheet styles both alike.
/// </summary>
/// <remarks>
/// IMPORTANT: element and attribute names are constants of this class, and every text and attribute value that comes
/// from the document is HTML-encoded (<see cref="Text"/>, <see cref="Attribute"/>), so nothing from the Markdown can
/// become markup. The one string of markup written whole is the MathML of <see cref="MathMlWriter"/>, which holds to the
/// same rule.
/// </remarks>
internal sealed partial class HtmlWriter
{
    private readonly StringBuilder _html;
    private readonly MarkdownHtmlOptions _options;
    private readonly HashSet<Type> _written = [];

    public HtmlWriter(StringBuilder html, MarkdownHtmlOptions options)
    {
        _html = html;
        _options = options;
    }

    /// <summary>The node types written so far, for the test that every node type of the model is written.</summary>
    public IReadOnlySet<Type> WrittenNodeTypes => _written;

    public void Document(DocumentNode document)
    {
        _html.Append("<div class=\"pmd\">");
        foreach (var block in document.Blocks)
        {
            Block(block);
        }
        _html.Append("</div>");
    }

    private void Block(BlockNode block)
    {
        var written = true;
        switch (block)
        {
            case ParagraphNode paragraph:
                Element("p", paragraph.Inlines);
                break;
            case HeadingNode heading:
                Heading(heading);
                break;
            case ListNode list:
                List(list);
                break;
            case CodeBlockNode code:
                CodeBlock(code);
                break;
            case DiagramNode diagram:
                Diagram(diagram);
                break;
            case MathBlockNode math:
                _html.Append("<div class=\"pmd-math\">");
                Formula(math.Formula, math.Source, display: true);
                _html.Append("</div>");
                break;
            case QuoteNode quote:
                _html.Append("<blockquote>");
                Blocks(quote.Blocks);
                _html.Append("</blockquote>");
                break;
            case AlertNode alert:
                Alert(alert);
                break;
            case AlignedBlockNode aligned:
                _html.Append("<div class=\"").Append(ViewerMarkup.AlignedBlockClass(aligned.Alignment)).Append("\">");
                Blocks(aligned.Blocks);
                _html.Append("</div>");
                break;
            case DetailsNode details:
                _html.Append(details.IsOpen ? "<details class=\"pmd-details\" open>" : "<details class=\"pmd-details\">");
                _html.Append("<summary>");
                Inlines(details.Summary);
                _html.Append("</summary>");
                Blocks(details.Blocks);
                _html.Append("</details>");
                break;
            case TableNode table:
                Table(table);
                break;
            case ThematicBreakNode:
                _html.Append("<hr>");
                break;
            case FootnotesNode footnotes:
                Footnotes(footnotes);
                break;
            default:
                written = false;
                break;
        }

        // NOTE: a node type is recorded only when a case above wrote it, so the node coverage test fails for a type that
        // falls through.
        if (written)
        {
            _written.Add(block.GetType());
        }
    }

    private void Blocks(IReadOnlyList<BlockNode> blocks)
    {
        foreach (var block in blocks)
        {
            Block(block);
        }
    }

    private void Element(string name, IReadOnlyList<InlineNode> inlines)
    {
        _html.Append('<').Append(name).Append('>');
        Inlines(inlines);
        _html.Append("</").Append(name).Append('>');
    }

    /// <summary>
    /// Writes a heading, with its id as an HTML id, which is what a link to it (<c>#id</c>) moves to with no script. The
    /// Blazor viewer leaves the id out and moves there itself, because a host page may have the same id.
    /// </summary>
    private void Heading(HeadingNode heading)
    {
        var name = "h" + Math.Clamp(heading.Level, 1, 6).ToString(CultureInfo.InvariantCulture);
        _html.Append('<').Append(name);
        if (heading.Id.Length > 0)
        {
            Attribute("id", heading.Id);
        }
        _html.Append('>');
        Inlines(heading.Inlines);
        _html.Append("</").Append(name).Append('>');
    }

    private void List(ListNode list)
    {
        var name = list.IsOrdered ? "ol" : "ul";
        _html.Append('<').Append(name);
        if (list.IsOrdered && list.Start != 1)
        {
            Attribute("start", list.Start.ToString(CultureInfo.InvariantCulture));
        }
        if (list.IsLoose)
        {
            _html.Append(" class=\"pmd-loose\"");
        }
        _html.Append('>');
        foreach (var item in list.Items)
        {
            if (item.IsChecked is { } isChecked)
            {
                // As in the viewer: a disabled box showing the state written in the text, with the check mark drawn over
                // it, because the stylesheet turns the browser's own off.
                _html.Append("<li class=\"pmd-task\">");
                _html.Append(isChecked ? "<input type=\"checkbox\" disabled checked>" : "<input type=\"checkbox\" disabled>");
                if (isChecked)
                {
                    _html.Append("<svg viewBox=\"0 0 14 14\" aria-hidden=\"true\"><path");
                    Attribute("d", TaskGlyphs.CheckPath);
                    _html.Append("></path></svg>");
                }
            }
            else
            {
                _html.Append("<li>");
            }
            Blocks(item.Blocks);
            _html.Append("</li>");
            _written.Add(typeof(ListItemNode));
        }
        _html.Append("</").Append(name).Append('>');
    }

    private void Table(TableNode table)
    {
        _html.Append("<div class=\"pmd-table\"><table>");
        Rows("thead", "th", table.HeaderRows, table.Alignments);
        Rows("tbody", "td", table.Rows, table.Alignments);
        _html.Append("</table></div>");
    }

    private void Rows(string section, string cellElement, IReadOnlyList<TableRowNode> rows, IReadOnlyList<ColumnAlignment> alignments)
    {
        if (rows.Count == 0)
        {
            return;
        }

        _html.Append('<').Append(section).Append('>');
        foreach (var row in rows)
        {
            _html.Append("<tr>");
            for (var column = 0; column < alignments.Count; column++)
            {
                _html.Append('<').Append(cellElement);
                if (ViewerMarkup.TextAlign(alignments[column]) is { } align)
                {
                    Attribute("style", "text-align:" + align);
                }
                _html.Append('>');
                if (column < row.Cells.Count)
                {
                    Inlines(row.Cells[column].Inlines);
                    _written.Add(typeof(TableCellNode));
                }
                _html.Append("</").Append(cellElement).Append('>');
            }
            _html.Append("</tr>");
            _written.Add(typeof(TableRowNode));
        }
        _html.Append("</").Append(section).Append('>');
    }

    private void Alert(AlertNode alert)
    {
        _html.Append("<div");
        Attribute("class", ViewerMarkup.AlertClass(alert.Kind));
        _html.Append(" role=\"note\"><p class=\"pmd-alert-title\"><svg viewBox=\"0 0 16 16\" aria-hidden=\"true\"><path fill-rule=\"evenodd\"");
        Attribute("d", AlertGlyphs.IconPath(alert.Kind));
        _html.Append("></path></svg>");
        Text(AlertGlyphs.Title(alert.Kind));
        _html.Append("</p>");
        Blocks(alert.Blocks);
        _html.Append("</div>");
    }

    /// <summary>Writes the footnotes as a numbered list, each note with its id, as the Blazor viewer does.</summary>
    private void Footnotes(FootnotesNode footnotes)
    {
        _html.Append("<section class=\"pmd-footnotes\"><ol>");
        foreach (var note in footnotes.Notes)
        {
            _html.Append("<li");
            Attribute("id", note.Id);
            _html.Append('>');
            Blocks(note.Blocks);
            _html.Append("</li>");
            _written.Add(typeof(FootnoteNode));
        }
        _html.Append("</ol></section>");
    }

    /// <summary>Writes text as element content, HTML-encoded.</summary>
    private void Text(string text) => _html.Append(WebUtility.HtmlEncode(text));

    /// <summary>Writes an attribute, its value HTML-encoded, after a space.</summary>
    private void Attribute(string name, string value) =>
        _html.Append(' ').Append(name).Append("=\"").Append(WebUtility.HtmlEncode(value)).Append('"');
}
