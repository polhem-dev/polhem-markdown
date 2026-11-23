using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Markdown.Diagrams;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

/// <summary>
/// Writes the model of a <see cref="MarkdownViewer"/> into its render tree. Text is always added as content, which
/// Blazor encodes, never as markup.
/// </summary>
/// <remarks>
/// NOTE: each node is written in its own region, so the sequence numbers below are literals per call site and stay
/// increasing within a region however deeply the nodes nest; that is what lets Blazor's diff match them up.
/// </remarks>
internal sealed class MarkdownRenderTree
{
    private const string CopyIcon =
        "M8 2h9a2 2 0 0 1 2 2v11h-2V4H8V2zM5 6h9a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2zm0 2v12h9V8H5z";

    private const string ClassAttribute = "class";

    private const string OnClickAttribute = "onclick";

    private const string DoneIcon = "M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z";

    private readonly MarkdownViewer _viewer;
    private readonly RenderTreeBuilder _builder;
    private readonly HashSet<Type> _rendered = [];
    private readonly HashSet<Type> _renderedShapes = [];

    public MarkdownRenderTree(MarkdownViewer viewer, RenderTreeBuilder builder)
    {
        _viewer = viewer;
        _builder = builder;
    }

    public IReadOnlySet<Type> RenderedNodeTypes => _rendered;

    public IReadOnlySet<Type> RenderedShapeTypes => _renderedShapes;

    public void Document(DocumentNode document, string? extraClass)
    {
        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, string.IsNullOrWhiteSpace(extraClass) ? "pmd" : "pmd " + extraClass);
        Blocks(2, document.Blocks);
        _builder.CloseElement();
    }

    private void Block(BlockNode block)
    {
        _builder.OpenRegion(0);
        var drawn = true;
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
                Diagram(diagram.Drawing);
                break;
            case QuoteNode quote:
                _builder.OpenElement(0, "blockquote");
                Blocks(1, quote.Blocks);
                _builder.CloseElement();
                break;
            case AlertNode alert:
                Alert(alert);
                break;
            case TableNode table:
                Table(table);
                break;
            case ThematicBreakNode:
                _builder.OpenElement(0, "hr");
                _builder.CloseElement();
                break;
            default:
                drawn = false;
                break;
        }

        // NOTE: a node type is recorded only when a case above drew it, so the node coverage test fails for a type
        // that falls through.
        if (drawn)
        {
            _rendered.Add(block.GetType());
        }
        _builder.CloseRegion();
    }

    /// <summary>
    /// Writes a heading. One with an id can receive focus from script only, which is how a link to it moves there: focus
    /// scrolls the heading into view and puts a screen reader at it. The id is not written as an HTML id, which could
    /// clash with the host page or with a second viewer on it.
    /// </summary>
    private void Heading(HeadingNode heading)
    {
        _builder.OpenElement(0, "h" + Math.Clamp(heading.Level, 1, 6).ToString(CultureInfo.InvariantCulture));
        if (heading.Id.Length > 0)
        {
            var id = heading.Id;

            // NOTE: Blazor calls a reference capture only when it creates the element, not when it updates one. Keying
            // the element by its id recreates it when the id changes, as it does while a heading's text arrives, so the
            // viewer never holds an element under an id it no longer has.
            _builder.SetKey(id);
            _builder.AddAttribute(1, "tabindex", "-1");
            _builder.AddElementReferenceCapture(2, element => _viewer.SetHeadingElement(id, element));
        }
        Inlines(3, heading.Inlines);
        _builder.CloseElement();
    }

    private void List(ListNode list)
    {
        _builder.OpenElement(0, list.IsOrdered ? "ol" : "ul");
        if (list.IsOrdered && list.Start != 1)
        {
            _builder.AddAttribute(1, "start", list.Start);
        }
        if (list.IsLoose)
        {
            _builder.AddAttribute(2, ClassAttribute, "pmd-loose");
        }
        foreach (var item in list.Items)
        {
            _builder.OpenRegion(3);
            _builder.OpenElement(0, "li");
            if (item.IsChecked is { } isChecked)
            {
                // The check box shows the state written in the text; it is disabled because a click changes nothing.
                _builder.AddAttribute(1, ClassAttribute, "pmd-task");
                _builder.OpenElement(2, "input");
                _builder.AddAttribute(3, "type", "checkbox");
                _builder.AddAttribute(4, "disabled", true);
                _builder.AddAttribute(5, "checked", isChecked);
                _builder.CloseElement();
            }
            Blocks(6, item.Blocks);
            _builder.CloseElement();
            _builder.CloseRegion();
            _rendered.Add(typeof(ListItemNode));
        }
        _builder.CloseElement();
    }

    private void CodeBlock(CodeBlockNode code)
    {
        var copied = _viewer.IsCopied(code);

        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, "pmd-code");

        _builder.OpenElement(2, "div");
        _builder.AddAttribute(3, ClassAttribute, "pmd-code-header");
        _builder.OpenElement(4, "span");
        _builder.AddAttribute(5, ClassAttribute, "pmd-code-lang");
        _builder.AddContent(6, code.Language);
        _builder.CloseElement();

        _builder.OpenElement(7, "button");
        _builder.AddAttribute(8, "type", "button");
        _builder.AddAttribute(9, ClassAttribute, "pmd-copy");
        _builder.AddAttribute(10, "title", _viewer.CopyCodeLabel);
        _builder.AddAttribute(11, "aria-label", _viewer.CopyCodeLabel);
        _builder.AddAttribute(12, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.CopyAsync(code)));
        _builder.OpenElement(13, "svg");
        _builder.AddAttribute(14, "viewBox", "0 0 24 24");
        _builder.AddAttribute(15, "aria-hidden", "true");
        _builder.OpenElement(16, "path");
        _builder.AddAttribute(17, "d", copied ? DoneIcon : CopyIcon);
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseElement();

        _builder.OpenElement(18, "pre");
        _builder.OpenElement(19, "code");
        _builder.AddContent(20, code.Code);
        _builder.CloseElement();
        _builder.CloseElement();

        _builder.CloseElement();
    }

    private void Alert(AlertNode alert)
    {
        var kind = alert.Kind.ToString().ToLowerInvariant();
        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, "pmd-alert pmd-alert-" + kind);
        _builder.OpenElement(2, "p");
        _builder.AddAttribute(3, ClassAttribute, "pmd-alert-title");
        _builder.OpenElement(4, "svg");
        _builder.AddAttribute(5, "viewBox", "0 0 16 16");
        _builder.AddAttribute(6, "aria-hidden", "true");
        _builder.OpenElement(7, "path");
        _builder.AddAttribute(8, "fill-rule", "evenodd");
        _builder.AddAttribute(9, "d", AlertGlyphs.IconPath(alert.Kind));
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.AddContent(10, AlertGlyphs.Title(alert.Kind));
        _builder.CloseElement();
        Blocks(11, alert.Blocks);
        _builder.CloseElement();
    }

    /// <summary>
    /// Writes a diagram as inline SVG at the drawing's own size; the stylesheet scales it down when it is wider than the
    /// viewer. Colors come from classes, so they follow the <c>--pmd-*</c> variables and the text color.
    /// </summary>
    private void Diagram(DiagramDrawing drawing)
    {
        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, "pmd-diagram");
        _builder.OpenElement(2, "svg");
        _builder.AddAttribute(3, "viewBox", "0 0 " + Number(drawing.Width) + " " + Number(drawing.Height));
        _builder.AddAttribute(4, "width", Number(drawing.Width));
        _builder.AddAttribute(5, "height", Number(drawing.Height));
        _builder.AddAttribute(6, "font-size", Number(DiagramDrawing.FontSize));
        _builder.AddAttribute(7, "stroke-width", Number(DiagramDrawing.OutlineThickness));
        foreach (var shape in drawing.Shapes)
        {
            _builder.OpenRegion(8);
            Shape(shape);
            _builder.CloseRegion();
        }
        _builder.CloseElement();
        _builder.CloseElement();
    }

    private void Shape(DiagramShape shape)
    {
        switch (shape)
        {
            case DiagramRectangle rectangle:
                _builder.OpenElement(0, "rect");
                _builder.AddAttribute(1, ClassAttribute, "pmd-d-box");
                _builder.AddAttribute(2, "x", Number(rectangle.X));
                _builder.AddAttribute(3, "y", Number(rectangle.Y));
                _builder.AddAttribute(4, "width", Number(rectangle.Width));
                _builder.AddAttribute(5, "height", Number(rectangle.Height));
                if (rectangle.CornerRadius > 0)
                {
                    _builder.AddAttribute(6, "rx", Number(rectangle.CornerRadius));
                }
                _builder.CloseElement();
                break;
            case DiagramPolygon polygon:
                _builder.OpenElement(7, "polygon");
                _builder.AddAttribute(8, ClassAttribute, polygon.IsArrowHead ? "pmd-d-arrow" : "pmd-d-box");
                _builder.AddAttribute(9, "points", Points(polygon.Points));
                _builder.CloseElement();
                break;
            case DiagramLine line:
                _builder.OpenElement(10, "polyline");
                _builder.AddAttribute(11, ClassAttribute, line.Stroke == DiagramStroke.Rule ? "pmd-d-line pmd-d-rule" : "pmd-d-line");
                _builder.AddAttribute(12, "points", Points(line.Points));
                _builder.AddAttribute(13, "stroke-width", Number(line.Thickness));
                if (line.Dash > 0)
                {
                    _builder.AddAttribute(14, "stroke-dasharray", Number(line.Dash));
                }
                _builder.CloseElement();
                break;
            case DiagramText text:
                _builder.OpenElement(15, "text");
                _builder.AddAttribute(16, "x", Number(text.X));
                _builder.AddAttribute(17, "y", Number(text.Y));
                _builder.AddContent(18, text.Text);
                _builder.CloseElement();
                break;
            default:
                // NOTE: a shape type is recorded only when a case above drew it, so the shape coverage test fails for a
                // type added to the drawing that this viewer does not draw.
                return;
        }
        _renderedShapes.Add(shape.GetType());
    }

    private static string Points(IReadOnlyList<DiagramPoint> points) =>
        string.Join(' ', points.Select(point => Number(point.X) + "," + Number(point.Y)));

    private static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private void Table(TableNode table)
    {
        _rendered.Add(typeof(TableRowNode));
        _rendered.Add(typeof(TableCellNode));

        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, ClassAttribute, "pmd-table");
        _builder.OpenElement(2, "table");
        Rows(3, "thead", "th", table.HeaderRows, table.Alignments);
        Rows(4, "tbody", "td", table.Rows, table.Alignments);
        _builder.CloseElement();
        _builder.CloseElement();
    }

    private void Rows(int sequence, string section, string cellElement, IReadOnlyList<TableRowNode> rows, IReadOnlyList<ColumnAlignment> alignments)
    {
        if (rows.Count == 0)
        {
            return;
        }

        _builder.OpenRegion(sequence);
        _builder.OpenElement(0, section);
        foreach (var cells in rows.Select(row => row.Cells))
        {
            _builder.OpenRegion(1);
            _builder.OpenElement(0, "tr");
            for (var column = 0; column < alignments.Count; column++)
            {
                _builder.OpenRegion(1);
                _builder.OpenElement(0, cellElement);
                if (Alignment(alignments[column]) is { } align)
                {
                    _builder.AddAttribute(1, "style", "text-align:" + align);
                }
                if (column < cells.Count)
                {
                    Inlines(2, cells[column].Inlines);
                }
                _builder.CloseElement();
                _builder.CloseRegion();
            }
            _builder.CloseElement();
            _builder.CloseRegion();
        }
        _builder.CloseElement();
        _builder.CloseRegion();
    }

    private static string? Alignment(ColumnAlignment alignment) => alignment switch
    {
        ColumnAlignment.Left => "left",
        ColumnAlignment.Center => "center",
        ColumnAlignment.Right => "right",
        _ => null,
    };

    private void Element(string name, IReadOnlyList<InlineNode> inlines)
    {
        _builder.OpenElement(0, name);
        Inlines(1, inlines);
        _builder.CloseElement();
    }

    /// <summary>Writes child blocks in a region of their own, after the parent's attributes.</summary>
    private void Blocks(int sequence, IReadOnlyList<BlockNode> blocks)
    {
        _builder.OpenRegion(sequence);
        foreach (var block in blocks)
        {
            Block(block);
        }
        _builder.CloseRegion();
    }

    /// <summary>Writes inlines in a region of their own, after the parent's attributes.</summary>
    private void Inlines(int sequence, IReadOnlyList<InlineNode> inlines)
    {
        _builder.OpenRegion(sequence);
        foreach (var inline in inlines)
        {
            Inline(inline);
        }
        _builder.CloseRegion();
    }

    private void Inline(InlineNode inline)
    {
        _builder.OpenRegion(0);
        var drawn = true;
        switch (inline)
        {
            case TextNode text:
                _builder.AddContent(0, text.Text);
                break;
            case CodeSpanNode code:
                _builder.OpenElement(1, "code");
                _builder.AddContent(2, code.Code);
                _builder.CloseElement();
                break;
            case EmphasisNode emphasis:
                Element(emphasis.Kind switch
                {
                    EmphasisKind.Bold => "strong",
                    EmphasisKind.Strikethrough => "del",
                    _ => "em",
                }, emphasis.Inlines);
                break;
            case LinkNode link:
                Link(link);
                break;
            case AnchorLinkNode anchor:
                AnchorLink(anchor);
                break;
            case LineBreakNode:
                _builder.OpenElement(3, "br");
                _builder.CloseElement();
                break;
            default:
                drawn = false;
                break;
        }
        if (drawn)
        {
            _rendered.Add(inline.GetType());
        }
        _builder.CloseRegion();
    }

    /// <summary>
    /// Writes a link to a heading of the text. The click never navigates: with a <c>base</c> element in the page, the
    /// <c>#id</c> in the href would resolve against the site root instead of the current page.
    /// </summary>
    private void AnchorLink(AnchorLinkNode anchor)
    {
        var id = anchor.Id;
        _builder.OpenElement(0, "a");
        _builder.AddAttribute(1, "href", "#" + id);
        _builder.AddAttribute(2, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.FocusHeadingAsync(id)));
        _builder.AddEventPreventDefaultAttribute(3, OnClickAttribute, true);
        Inlines(4, anchor.Inlines);
        _builder.CloseElement();
    }

    private void Link(LinkNode link)
    {
        var target = link.Target;
        _builder.OpenElement(0, "a");
        _builder.AddAttribute(1, "href", target.AbsoluteUri);
        if (_viewer.HandlesLinks)
        {
            _builder.AddAttribute(2, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.ActivateLinkAsync(target)));
            _builder.AddEventPreventDefaultAttribute(3, OnClickAttribute, true);
        }
        else
        {
            _builder.AddAttribute(4, "target", "_blank");
            _builder.AddAttribute(5, "rel", "noopener noreferrer");
        }
        Inlines(6, link.Inlines);
        _builder.CloseElement();
    }
}
