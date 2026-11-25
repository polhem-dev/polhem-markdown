using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
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
internal sealed partial class MarkdownRenderTree
{
    private const string ClassAttribute = "class";

    private const string OnClickAttribute = "onclick";

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
        _builder.AddElementReferenceCapture(2, _viewer.SetRootElement);

        // NOTE: each top-level block is one element, a child of this one, which is how the script finds the element of a
        // block from its index in the source ranges the viewer gives it (MarkdownViewer.Source.cs).
        Blocks(3, document.Blocks);
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
            case MathBlockNode math:
                MathBlock(math);
                break;
            case QuoteNode quote:
                _builder.OpenElement(0, "blockquote");
                Blocks(1, quote.Blocks);
                _builder.CloseElement();
                break;
            case AlertNode alert:
                Alert(alert);
                break;
            case AlignedBlockNode aligned:
                _builder.OpenElement(0, "div");
                _builder.AddAttribute(1, ClassAttribute, aligned.Alignment == BlockAlignment.Right ? "pmd-align-right" : "pmd-align-center");
                Blocks(2, aligned.Blocks);
                _builder.CloseElement();
                break;
            case DetailsNode details:
                Details(details);
                break;
            case TableNode table:
                Table(table);
                break;
            case ThematicBreakNode:
                _builder.OpenElement(0, "hr");
                _builder.CloseElement();
                break;
            case FootnotesNode footnotes:
                Footnotes(footnotes);
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
            _builder.AddElementReferenceCapture(2, element => _viewer.SetAnchorElement(id, element));
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
                if (isChecked)
                {
                    // The browser's own check mark is turned off by the stylesheet, so that the box looks the same as in
                    // the Avalonia viewer; this one is drawn over it.
                    _builder.OpenElement(6, "svg");
                    _builder.AddAttribute(7, "viewBox", "0 0 14 14");
                    _builder.AddAttribute(8, "aria-hidden", "true");
                    _builder.OpenElement(9, "path");
                    _builder.AddAttribute(10, "d", TaskGlyphs.CheckPath);
                    _builder.CloseElement();
                    _builder.CloseElement();
                }
            }
            Blocks(11, item.Blocks);
            _builder.CloseElement();
            _builder.CloseRegion();
            _rendered.Add(typeof(ListItemNode));
        }
        _builder.CloseElement();
    }

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
                    EmphasisKind.Superscript => "sup",
                    EmphasisKind.Subscript => "sub",
                    EmphasisKind.Keyboard => "kbd",
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
            case ImageNode image:
                Image(image);
                break;
            case MathNode math:
                Formula(math.Formula, math.Source, display: false);
                break;
            case FootnoteReferenceNode reference:
                FootnoteReference(reference);
                break;
            case FootnoteBackLinkNode back:
                FootnoteBackLink(back);
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
    /// Writes a link to a heading of the text. Once the component is interactive the click never navigates; the viewer
    /// scrolls to the heading itself (see <see cref="MarkdownViewer.AnchorHref"/> for the href).
    /// </summary>
    private void AnchorLink(AnchorLinkNode anchor)
    {
        var id = anchor.Id;
        _builder.OpenElement(0, "a");
        _builder.AddAttribute(1, "href", _viewer.AnchorHref(id));
        _builder.AddAttribute(2, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.MoveToAnchorAsync(id)));
        _builder.AddEventPreventDefaultAttribute(3, OnClickAttribute, true);
        Inlines(4, anchor.Inlines);
        _builder.CloseElement();
    }

    /// <summary>
    /// Writes a section the reader opens and closes. The browser keeps it open or closed as the reader leaves it; a new
    /// render writes <c>open</c> only when the text changes it.
    /// </summary>
    private void Details(DetailsNode details)
    {
        _builder.OpenElement(0, "details");
        _builder.AddAttribute(1, ClassAttribute, "pmd-details");
        _builder.AddAttribute(2, "open", details.IsOpen);
        _builder.OpenElement(3, "summary");
        Inlines(4, details.Summary);
        _builder.CloseElement();
        Blocks(5, details.Blocks);
        _builder.CloseElement();
    }

    /// <summary>
    /// Writes an image, inside a <c>picture</c> when it has a source for a dark or a light theme, which the browser picks
    /// by the reader's color scheme.
    /// </summary>
    private void Image(ImageNode image)
    {
        if (image.DarkSource is null && image.LightSource is null)
        {
            Img(image);
            return;
        }
        _builder.OpenElement(10, "picture");
        ThemeSource(11, "(prefers-color-scheme: dark)", image.DarkSource);
        ThemeSource(12, "(prefers-color-scheme: light)", image.LightSource);
        _builder.OpenRegion(13);
        Img(image);
        _builder.CloseRegion();
        _builder.CloseElement();
    }

    private void ThemeSource(int sequence, string media, Uri? source)
    {
        if (source is null)
        {
            return;
        }
        _builder.OpenRegion(sequence);
        _builder.OpenElement(0, "source");
        _builder.AddAttribute(1, "media", media);
        _builder.AddAttribute(2, "srcset", source.AbsoluteUri);
        _builder.CloseElement();
        _builder.CloseRegion();
    }

    /// <summary>
    /// Writes an <c>img</c>. The browser loads it lazily, sends no referrer, and in an <c>img</c> an SVG's scripts do not
    /// run. A broken image shows its alternative text.
    /// </summary>
    private void Img(ImageNode image)
    {
        _builder.OpenElement(0, "img");
        _builder.AddAttribute(1, ClassAttribute, "pmd-img");
        _builder.AddAttribute(2, "src", image.Source.AbsoluteUri);
        _builder.AddAttribute(3, "alt", image.AlternativeText);
        if (image.Title is { } title)
        {
            _builder.AddAttribute(4, "title", title);
        }
        _builder.AddAttribute(5, "loading", "lazy");
        _builder.AddAttribute(6, "decoding", "async");
        _builder.AddAttribute(7, "referrerpolicy", "no-referrer");
        if (image.Width is { } width)
        {
            _builder.AddAttribute(8, "width", width);
        }
        if (image.Height is { } height)
        {
            _builder.AddAttribute(9, "height", height);

            // NOTE: with a width the stylesheet lets the height follow the image's proportions, so a narrow viewer
            // shrinks both; the height still bounds it, and the image fits inside both as in the Avalonia viewer.
            if (image.Width is not null)
            {
                _builder.AddAttribute(10, "style", "max-height:" + height.ToString(CultureInfo.InvariantCulture) + "px");
            }
        }
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
