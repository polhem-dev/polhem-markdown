using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Builds the controls of a <see cref="MarkdownViewer"/> from the model. Colors and the code font are bound to the
/// viewer's properties, so changing them needs no rebuild.
/// </summary>
internal sealed class MarkdownRenderer
{
    private static readonly string[] s_bullets = ["•", "◦", "▪"];
    private static readonly double[] s_headingScale = [1.5, 1.3, 1.15, 1.0, 1.0, 1.0];

    private readonly MarkdownViewer _viewer;
    private readonly HashSet<Type> _rendered = [];

    public MarkdownRenderer(MarkdownViewer viewer)
    {
        _viewer = viewer;
    }

    /// <summary>The node types rendered so far.</summary>
    public IReadOnlySet<Type> RenderedNodeTypes => _rendered;

    public Control Block(BlockNode block, int depth)
    {
        Control? control = block switch
        {
            ParagraphNode paragraph => Text(paragraph.Inlines),
            HeadingNode heading => Heading(heading),
            ListNode list => List(list, depth),
            CodeBlockNode code => new CodeBlockView(_viewer, code),
            QuoteNode quote => Quote(quote, depth),
            AlertNode alert => Alert(alert, depth),
            TableNode table => Table(table),
            ThematicBreakNode => Rule(),
            _ => null,
        };

        // NOTE: a node type is recorded only when a case above drew it, so the node coverage test fails for a type
        // that falls through to the empty placeholder.
        if (control is null)
        {
            return Text([]);
        }
        _rendered.Add(block.GetType());
        return control;
    }

    private MarkdownTextBlock Heading(HeadingNode heading)
    {
        var text = Text(heading.Inlines);
        text.FontWeight = FontWeight.SemiBold;
        text.FontSize = _viewer.GetValue(TextElement.FontSizeProperty) * s_headingScale[Math.Clamp(heading.Level, 1, 6) - 1];
        return text;
    }

    private Grid List(ListNode list, int depth)
    {
        _rendered.Add(typeof(ListItemNode));
        var grid = new Grid { RowSpacing = list.IsLoose ? 8 : 2 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var index = 0; index < list.Items.Count; index++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var item = list.Items[index];
            Control marker = item.IsChecked is { } isChecked
                ? TaskCheckBox(isChecked)
                : new TextBlock
                {
                    Text = list.IsOrdered
                        ? (list.Start + index).ToString(System.Globalization.CultureInfo.InvariantCulture) + "."
                        : s_bullets[depth % s_bullets.Length],
                    Margin = new Thickness(0, 0, 6, 0),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    MinWidth = list.IsOrdered ? 18 : 12,
                    TextAlignment = TextAlignment.Right,
                };
            Grid.SetRow(marker, index);

            var content = Blocks(item.Blocks, depth + 1, spacing: list.IsLoose ? 8 : 4);
            Grid.SetRow(content, index);
            Grid.SetColumn(content, 1);

            grid.Children.Add(marker);
            grid.Children.Add(content);
        }
        return grid;
    }

    /// <summary>The check box of a task list item, which shows the state written in the text and cannot be changed.</summary>
    private static CheckBox TaskCheckBox(bool isChecked) => new()
    {
        IsChecked = isChecked,
        IsEnabled = false,
        MinWidth = 0,
        MinHeight = 0,
        Padding = new Thickness(0),
        Margin = new Thickness(0, 0, 6, 0),
        VerticalAlignment = VerticalAlignment.Top,
    };

    private Border Quote(QuoteNode quote, int depth)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 0, 2),
            Child = Blocks(quote.Blocks, depth, spacing: 8),
        };
        border.Bind(Border.BorderBrushProperty, _viewer.GetObservable(MarkdownViewer.RuleBrushProperty));
        return border;
    }

    private Border Alert(AlertNode alert, int depth)
    {
        var brush = _viewer.GetObservable(alert.Kind switch
        {
            AlertKind.Tip => MarkdownViewer.AlertTipBrushProperty,
            AlertKind.Important => MarkdownViewer.AlertImportantBrushProperty,
            AlertKind.Warning => MarkdownViewer.AlertWarningBrushProperty,
            AlertKind.Caution => MarkdownViewer.AlertCautionBrushProperty,
            _ => MarkdownViewer.AlertNoteBrushProperty,
        });

        var icon = new global::Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("F0 " + AlertGlyphs.IconPath(alert.Kind)),
            Width = 16,
            Height = 16,
            VerticalAlignment = VerticalAlignment.Center,
        };
        icon.Bind(global::Avalonia.Controls.Shapes.Shape.FillProperty, brush);

        var title = new TextBlock
        {
            Text = AlertGlyphs.Title(alert.Kind),
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.Bind(TextBlock.ForegroundProperty, brush);

        var content = Blocks(alert.Blocks, depth, spacing: 8);
        content.Children.Insert(0, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { icon, title } });

        var border = new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 0, 2),
            Child = content,
        };
        border.Bind(Border.BorderBrushProperty, brush);
        return border;
    }

    private ScrollViewer Table(TableNode table)
    {
        _rendered.Add(typeof(TableRowNode));
        _rendered.Add(typeof(TableCellNode));

        var columns = table.Alignments.Count;
        var grid = new Grid();
        for (var column = 0; column < columns; column++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        }

        var rows = table.HeaderRows.Select(row => (row, header: true)).Concat(table.Rows.Select(row => (row, header: false))).ToList();
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var (row, header) = rows[rowIndex];
            for (var column = 0; column < columns; column++)
            {
                var text = Text(column < row.Cells.Count ? row.Cells[column].Inlines : []);
                text.TextAlignment = table.Alignments[column] switch
                {
                    ColumnAlignment.Center => TextAlignment.Center,
                    ColumnAlignment.Right => TextAlignment.Right,
                    _ => TextAlignment.Left,
                };
                if (header)
                {
                    text.FontWeight = FontWeight.SemiBold;
                }

                var cell = new Border
                {
                    // Each cell draws its right and bottom edge; the first row and column add the outer top and left.
                    BorderThickness = new Thickness(column == 0 ? 1 : 0, rowIndex == 0 ? 1 : 0, 1, 1),
                    Padding = new Thickness(8, 4),
                    Child = text,
                };
                cell.Bind(Border.BorderBrushProperty, _viewer.GetObservable(MarkdownViewer.RuleBrushProperty));
                if (header)
                {
                    cell.Bind(Border.BackgroundProperty, _viewer.GetObservable(MarkdownViewer.CodeBackgroundProperty));
                }
                Grid.SetRow(cell, rowIndex);
                Grid.SetColumn(cell, column);
                grid.Children.Add(cell);
            }
        }

        return new ScrollViewer
        {
            HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = grid,
        };
    }

    private Border Rule()
    {
        var rule = new Border { Height = 1, Margin = new Thickness(0, 4) };
        rule.Bind(Border.BackgroundProperty, _viewer.GetObservable(MarkdownViewer.RuleBrushProperty));
        return rule;
    }

    private StackPanel Blocks(IReadOnlyList<BlockNode> blocks, int depth, double spacing)
    {
        var panel = new StackPanel { Spacing = spacing };
        foreach (var block in blocks)
        {
            panel.Children.Add(Block(block, depth));
        }
        return panel;
    }

    /// <summary>Builds selectable text from inlines, recording where each link lies in the text.</summary>
    public MarkdownTextBlock Text(IReadOnlyList<InlineNode> inlines)
    {
        var text = new MarkdownTextBlock(_viewer) { TextWrapping = TextWrapping.Wrap, Inlines = [] };
        var offset = 0;
        foreach (var inline in inlines)
        {
            text.Inlines!.Add(Inline(inline, text, ref offset));
        }
        return text;
    }

    private Inline Inline(InlineNode inline, MarkdownTextBlock owner, ref int offset)
    {
        if (DrawInline(inline, owner, ref offset) is { } drawn)
        {
            _rendered.Add(inline.GetType());
            return drawn;
        }
        return new Run("");
    }

    private Inline? DrawInline(InlineNode inline, MarkdownTextBlock owner, ref int offset)
    {
        switch (inline)
        {
            case TextNode text:
                offset += text.Text.Length;
                return new Run(text.Text);

            case CodeSpanNode code:
                offset += code.Code.Length;
                var run = new Run(code.Code);
                run.Bind(TextElement.FontFamilyProperty, _viewer.GetObservable(MarkdownViewer.CodeFontFamilyProperty));
                run.Bind(TextElement.BackgroundProperty, _viewer.GetObservable(MarkdownViewer.CodeBackgroundProperty));
                return run;

            case EmphasisNode emphasis:
                var span = emphasis.Kind switch
                {
                    EmphasisKind.Bold => new Bold(),
                    EmphasisKind.Strikethrough => new Span { TextDecorations = TextDecorations.Strikethrough },
                    _ => (Span)new Italic(),
                };
                foreach (var child in emphasis.Inlines)
                {
                    span.Inlines.Add(Inline(child, owner, ref offset));
                }
                return span;

            case LinkNode link:
                var start = offset;
                var linkSpan = new Span { TextDecorations = TextDecorations.Underline };
                linkSpan.Bind(TextElement.ForegroundProperty, _viewer.GetObservable(MarkdownViewer.LinkForegroundProperty));
                foreach (var child in link.Inlines)
                {
                    linkSpan.Inlines.Add(Inline(child, owner, ref offset));
                }
                owner.AddLink(start, offset - start, link.Target);
                return linkSpan;

            case LineBreakNode:
                offset += LineBreakLength;
                return new LineBreak();

            default:
                return null;
        }
    }

    /// <summary>
    /// The number of characters a <see cref="LineBreak"/> adds to the text of a text block: one, a line feed, on every
    /// platform. <c>Render_Link_RangeMatchesText</c> checks the link offsets it produces against
    /// <see cref="InlineCollection.Text"/>.
    /// </summary>
    internal const int LineBreakLength = 1;
}
