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
internal sealed class MarkdownRenderTree
{
    private const string CopyIcon =
        "M8 2h9a2 2 0 0 1 2 2v11h-2V4H8V2zM5 6h9a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V8a2 2 0 0 1 2-2zm0 2v12h9V8H5z";

    private const string DoneIcon = "M9 16.2 4.8 12l-1.4 1.4L9 19 21 7l-1.4-1.4z";

    private readonly MarkdownViewer _viewer;
    private readonly RenderTreeBuilder _builder;
    private readonly HashSet<Type> _rendered = [];

    public MarkdownRenderTree(MarkdownViewer viewer, RenderTreeBuilder builder)
    {
        _viewer = viewer;
        _builder = builder;
    }

    public IReadOnlySet<Type> RenderedNodeTypes => _rendered;

    public void Document(DocumentNode document, string? extraClass)
    {
        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, "class", string.IsNullOrWhiteSpace(extraClass) ? "pmd" : "pmd " + extraClass);
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
                Element("h" + Math.Clamp(heading.Level, 1, 6).ToString(CultureInfo.InvariantCulture), heading.Inlines);
                break;
            case ListNode list:
                List(list);
                break;
            case CodeBlockNode code:
                CodeBlock(code);
                break;
            case QuoteNode quote:
                _builder.OpenElement(0, "blockquote");
                Blocks(1, quote.Blocks);
                _builder.CloseElement();
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

    private void List(ListNode list)
    {
        _builder.OpenElement(0, list.IsOrdered ? "ol" : "ul");
        if (list.IsOrdered && list.Start != 1)
        {
            _builder.AddAttribute(1, "start", list.Start);
        }
        if (list.IsLoose)
        {
            _builder.AddAttribute(2, "class", "pmd-loose");
        }
        foreach (var item in list.Items)
        {
            _builder.OpenRegion(3);
            _builder.OpenElement(0, "li");
            Blocks(1, item.Blocks);
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
        _builder.AddAttribute(1, "class", "pmd-code");

        _builder.OpenElement(2, "div");
        _builder.AddAttribute(3, "class", "pmd-code-header");
        _builder.OpenElement(4, "span");
        _builder.AddAttribute(5, "class", "pmd-code-lang");
        _builder.AddContent(6, code.Language);
        _builder.CloseElement();

        _builder.OpenElement(7, "button");
        _builder.AddAttribute(8, "type", "button");
        _builder.AddAttribute(9, "class", "pmd-copy");
        _builder.AddAttribute(10, "title", _viewer.CopyCodeLabel);
        _builder.AddAttribute(11, "aria-label", _viewer.CopyCodeLabel);
        _builder.AddAttribute(12, "onclick", EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.CopyAsync(code)));
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

    private void Table(TableNode table)
    {
        _rendered.Add(typeof(TableRowNode));
        _rendered.Add(typeof(TableCellNode));

        _builder.OpenElement(0, "div");
        _builder.AddAttribute(1, "class", "pmd-table");
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
        foreach (var row in rows)
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
                if (column < row.Cells.Count)
                {
                    Inlines(2, row.Cells[column].Inlines);
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
                Element(emphasis.IsStrong ? "strong" : "em", emphasis.Inlines);
                break;
            case LinkNode link:
                Link(link);
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

    private void Link(LinkNode link)
    {
        var target = link.Target;
        _builder.OpenElement(0, "a");
        _builder.AddAttribute(1, "href", target.AbsoluteUri);
        if (_viewer.HandlesLinks)
        {
            _builder.AddAttribute(2, "onclick", EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.ActivateLinkAsync(target)));
            _builder.AddEventPreventDefaultAttribute(3, "onclick", true);
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
