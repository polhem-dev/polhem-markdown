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
    private readonly HashSet<Type> _renderedShapes = [];

    public MarkdownRenderer(MarkdownViewer viewer)
    {
        _viewer = viewer;
    }

    /// <summary>The node types rendered so far.</summary>
    public IReadOnlySet<Type> RenderedNodeTypes => _rendered;

    /// <summary>The diagram shape types drawn so far.</summary>
    public IReadOnlySet<Type> RenderedShapeTypes => _renderedShapes;

    public Control Block(BlockNode block, int depth)
    {
        Control? control = block switch
        {
            ParagraphNode paragraph => Text(paragraph.Inlines),
            HeadingNode heading => Heading(heading),
            ListNode list => List(list, depth),
            CodeBlockNode code => new CodeBlockView(_viewer, code),
            DiagramNode diagram => Diagram(diagram),
            QuoteNode quote => Quote(quote, depth),
            AlertNode alert => Alert(alert, depth),
            TableNode table => Table(table),
            ThematicBreakNode => Rule(),
            FootnotesNode footnotes => Footnotes(footnotes, depth),
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

    /// <summary>
    /// A diagram at its own size, centered, and scaled down to the viewer's width when it is wider, never up.
    /// </summary>
    private Viewbox Diagram(DiagramNode diagram) => new()
    {
        Stretch = Stretch.Uniform,
        StretchDirection = StretchDirection.DownOnly,
        HorizontalAlignment = HorizontalAlignment.Center,
        Child = new DiagramView(_viewer, diagram, _renderedShapes),
    };

    private MarkdownTextBlock Heading(HeadingNode heading)
    {
        var text = Text(heading.Inlines);
        text.FontWeight = FontWeight.SemiBold;
        text.FontSize = _viewer.GetValue(TextElement.FontSizeProperty) * s_headingScale[Math.Clamp(heading.Level, 1, 6) - 1];
        if (heading.Id.Length > 0)
        {
            text.AddAnchor(heading.Id);
        }
        return text;
    }

    /// <summary>
    /// The footnotes after a rule, as a numbered list in a slightly smaller font. Each note's content is the anchor its
    /// references move to.
    /// </summary>
    private StackPanel Footnotes(FootnotesNode footnotes, int depth)
    {
        _rendered.Add(typeof(FootnoteNode));
        var grid = new Grid { RowSpacing = 4 };
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
        grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        for (var index = 0; index < footnotes.Notes.Count; index++)
        {
            var note = footnotes.Notes[index];
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var marker = new TextBlock
            {
                Text = note.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".",
                Margin = new Thickness(0, 0, 6, 0),
                MinWidth = 18,
                TextAlignment = TextAlignment.Right,
            };
            Grid.SetRow(marker, index);

            var content = Blocks(note.Blocks, depth + 1, spacing: 4);
            content.SetValue(MarkdownViewer.AnchorIdsProperty, [note.Id]);
            Grid.SetRow(content, index);
            Grid.SetColumn(content, 1);

            grid.Children.Add(marker);
            grid.Children.Add(content);
        }

        var panel = new StackPanel { Spacing = 8, Children = { Rule(), grid } };
        panel.SetValue(TextElement.FontSizeProperty, _viewer.GetValue(TextElement.FontSizeProperty) * FootnoteScale);
        return panel;
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
            var marker = Marker(list, item, index, depth);
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

    /// <summary>The marker before a list item: a check box for a task, otherwise its number or a bullet.</summary>
    private static Control Marker(ListNode list, ListItemNode item, int index, int depth)
    {
        if (item.IsChecked is { } isChecked)
        {
            return TaskCheckBox(isChecked);
        }
        var text = list.IsOrdered
            ? (list.Start + index).ToString(System.Globalization.CultureInfo.InvariantCulture) + "."
            : s_bullets[depth % s_bullets.Length];
        return new TextBlock
        {
            Text = text,
            Margin = new Thickness(0, 0, 6, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
            MinWidth = list.IsOrdered ? 18 : 12,
            TextAlignment = TextAlignment.Right,
        };
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
                var cell = TableCell(column < row.Cells.Count ? row.Cells[column].Inlines : [], table.Alignments[column], header);

                // Each cell draws its right and bottom edge; the first row and column add the outer top and left.
                cell.BorderThickness = new Thickness(column == 0 ? 1 : 0, rowIndex == 0 ? 1 : 0, 1, 1);
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

    private Border TableCell(IReadOnlyList<InlineNode> inlines, ColumnAlignment alignment, bool header)
    {
        var text = Text(inlines);
        text.TextAlignment = alignment switch
        {
            ColumnAlignment.Center => TextAlignment.Center,
            ColumnAlignment.Right => TextAlignment.Right,
            _ => TextAlignment.Left,
        };
        if (header)
        {
            text.FontWeight = FontWeight.SemiBold;
        }

        var cell = new Border { Padding = new Thickness(8, 4), Child = text };
        cell.Bind(Border.BorderBrushProperty, _viewer.GetObservable(MarkdownViewer.RuleBrushProperty));
        if (header)
        {
            cell.Bind(Border.BackgroundProperty, _viewer.GetObservable(MarkdownViewer.CodeBackgroundProperty));
        }
        return cell;
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
            text.Inlines.Add(Inline(inline, text, ref offset));
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
                var linkSpan = LinkSpan(link.Inlines, owner, ref offset);
                owner.AddLink(start, offset - start, link.Target);
                return linkSpan;

            case AnchorLinkNode anchor:
                var anchorStart = offset;
                var anchorSpan = LinkSpan(anchor.Inlines, owner, ref offset);
                owner.AddAnchorLink(anchorStart, offset - anchorStart, anchor.Id);
                return anchorSpan;

            case LineBreakNode:
                offset += LineBreakLength;
                return new LineBreak();

            case FootnoteReferenceNode reference:
                var number = reference.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                owner.AddAnchorLink(offset, number.Length, reference.NoteId);
                owner.AddAnchor(reference.Id);
                offset += number.Length;
                var superscript = new Run(number)
                {
                    BaselineAlignment = BaselineAlignment.Superscript,
                    FontSize = _viewer.GetValue(TextElement.FontSizeProperty) * SuperscriptScale,
                };
                superscript.Bind(TextElement.ForegroundProperty, _viewer.GetObservable(MarkdownViewer.LinkForegroundProperty));
                return superscript;

            case FootnoteBackLinkNode back:
                // A space keeps the arrow off the last word, as on GitHub; it is not part of the link.
                offset += 1;
                var arrow = back.Position > 1
                    ? BackArrow + back.Position.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : BackArrow;
                owner.AddAnchorLink(offset, arrow.Length, back.ReferenceId);
                offset += arrow.Length;
                var arrowRun = new Run(arrow);
                arrowRun.Bind(TextElement.ForegroundProperty, _viewer.GetObservable(MarkdownViewer.LinkForegroundProperty));
                return new Span { Inlines = { new Run(" "), arrowRun } };

            default:
                return null;
        }
    }

    private Span LinkSpan(IReadOnlyList<InlineNode> inlines, MarkdownTextBlock owner, ref int offset)
    {
        var span = new Span { TextDecorations = TextDecorations.Underline };
        span.Bind(TextElement.ForegroundProperty, _viewer.GetObservable(MarkdownViewer.LinkForegroundProperty));
        foreach (var child in inlines)
        {
            span.Inlines.Add(Inline(child, owner, ref offset));
        }
        return span;
    }

    /// <summary>
    /// The number of characters a <see cref="LineBreak"/> adds to the text of a text block: one, a line feed, on every
    /// platform. <c>Render_Link_RangeMatchesText</c> checks the link offsets it produces against
    /// <see cref="InlineCollection.Text"/>.
    /// </summary>
    internal const int LineBreakLength = 1;

    private const string BackArrow = "\u21A9";
    private const double SuperscriptScale = 0.75;
    private const double FootnoteScale = 0.875;
}
