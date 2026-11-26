using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Markdown.Avalonia.Formulas;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// Builds the controls of a <see cref="MarkdownViewer"/> from the model. Colors and the code font are bound to the
/// viewer's properties, so changing them needs no rebuild.
/// </summary>
internal sealed partial class MarkdownRenderer
{
    private static readonly string[] s_bullets = ["•", "◦", "▪"];
    private static readonly double[] s_headingScale = [1.5, 1.3, 1.15, 1.0, 1.0, 1.0];

    private readonly MarkdownViewer _viewer;
    private readonly HashSet<Type> _rendered = [];
    private readonly HashSet<Type> _renderedShapes = [];
    private readonly HashSet<Type> _renderedFormulas = [];

    public MarkdownRenderer(MarkdownViewer viewer)
    {
        _viewer = viewer;
    }

    /// <summary>The node types rendered so far.</summary>
    public IReadOnlySet<Type> RenderedNodeTypes => _rendered;

    /// <summary>The diagram shape types drawn so far.</summary>
    public IReadOnlySet<Type> RenderedShapeTypes => _renderedShapes;

    /// <summary>The formula part types laid out so far.</summary>
    public IReadOnlySet<Type> RenderedFormulaTypes => _renderedFormulas;

    public Control Block(BlockNode block, int depth)
    {
        Control? control = block switch
        {
            ParagraphNode paragraph => Text(paragraph.Inlines),
            HeadingNode heading => Heading(heading),
            ListNode list => List(list, depth),
            CodeBlockNode code => new CodeBlockView(_viewer, code),
            DiagramNode diagram => Diagram(diagram),
            MathBlockNode math => MathBlock(math),
            QuoteNode quote => Quote(quote, depth),
            AlertNode alert => Alert(alert, depth),
            AlignedBlockNode aligned => Aligned(aligned, depth),
            DetailsNode details => Details(details, depth),
            TableNode table => Table(table),
            ThematicBreakNode => Expose(Rule(), AutomationControlType.Separator),
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

    /// <summary>A formula on its own, centered, scrolling sideways when it is wider than the viewer.</summary>
    private ScrollViewer MathBlock(MathBlockNode math) => new()
    {
        HorizontalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
        VerticalScrollBarVisibility = global::Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
        Content = Named(new FormulaView(math.Formula, display: true, _renderedFormulas, line: null) { HorizontalAlignment = HorizontalAlignment.Center }, math.Source),
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
        // NOTE: a header with a level is what Avalonia's macOS backend shows as a heading; Windows reads the level.
        AutomationProperties.SetControlTypeOverride(text, AutomationControlType.Header);
        AutomationProperties.SetHeadingLevel(text, Math.Clamp(heading.Level, 1, 6));
        return text;
    }

    /// <summary>
    /// The footnotes after a rule, as a numbered list in a slightly smaller font. Each note's content is the anchor its
    /// references move to.
    /// </summary>
    private StackPanel Footnotes(FootnotesNode footnotes, int depth)
    {
        _rendered.Add(typeof(FootnoteNode));
        var list = ListPanel(spacing: 4);
        for (var index = 0; index < footnotes.Notes.Count; index++)
        {
            var note = footnotes.Notes[index];
            var marker = new TextBlock
            {
                Text = note.Number.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".",
                Margin = new Thickness(0, 0, 6, 0),
                MinWidth = 18,
                TextAlignment = TextAlignment.Right,
            };
            var content = Blocks(note.Blocks, depth + 1, spacing: 4);
            content.SetValue(MarkdownViewer.AnchorIdsProperty, [note.Id]);
            list.Children.Add(ListItem(marker, content, index, footnotes.Notes.Count));
        }

        var panel = new StackPanel { Spacing = 8, Children = { Rule(), list } };
        panel.SetValue(TextElement.FontSizeProperty, _viewer.GetValue(TextElement.FontSizeProperty) * FootnoteScale);
        return panel;
    }

    private StackPanel List(ListNode list, int depth)
    {
        _rendered.Add(typeof(ListItemNode));
        var panel = ListPanel(spacing: list.IsLoose ? 8 : 2);
        for (var index = 0; index < list.Items.Count; index++)
        {
            var item = list.Items[index];
            var content = Blocks(item.Blocks, depth + 1, spacing: list.IsLoose ? 8 : 4);
            panel.Children.Add(ListItem(Marker(list, item, index, depth), content, index, list.Items.Count));
        }
        return panel;
    }

    /// <summary>
    /// The panel of a list's items. Each item is a grid of its own, so an assistive technology sees it as one list item
    /// with its marker; the marker column is shared, so the items' content lines up as in one grid.
    /// </summary>
    private static StackPanel ListPanel(double spacing)
    {
        var panel = Expose(new StackPanel { Spacing = spacing }, AutomationControlType.List);
        panel.SetValue(MarkdownViewer.TextGroupProperty, MarkdownViewer.TextGroup.List);
        Grid.SetIsSharedSizeScope(panel, true);
        return panel;
    }

    private static Grid ListItem(Control marker, Control content, int index, int count)
    {
        var item = Expose(new Grid(), AutomationControlType.ListItem);
        item.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto) { SharedSizeGroup = MarkerColumn });
        item.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        AutomationProperties.SetPositionInSet(item, index + 1);
        AutomationProperties.SetSizeOfSet(item, count);
        Grid.SetColumn(content, 1);
        item.Children.Add(marker);
        item.Children.Add(content);
        return item;
    }

    private const string MarkerColumn = "Marker";

    /// <summary>Names a control for an assistive technology.</summary>
    private static T Named<T>(T control, string name) where T : Control
    {
        AutomationProperties.SetName(control, name);
        return control;
    }

    /// <summary>
    /// Shows a panel to an assistive technology as an element of <paramref name="type"/>, which a panel is not by
    /// default, optionally named.
    /// </summary>
    private static T Expose<T>(T control, AutomationControlType type, string? name = null) where T : Control
    {
        AutomationProperties.SetControlTypeOverride(control, type);
        AutomationProperties.SetIsControlElementOverride(control, true);
        if (name is not null)
        {
            AutomationProperties.SetName(control, name);
        }
        return control;
    }

    /// <summary>
    /// The check box of a task list item, which shows the state written in the text and cannot be changed. It is drawn
    /// with the viewer's brushes instead of the theme's check box, so it looks the same as in the Blazor viewer.
    /// </summary>
    private CheckBox TaskCheckBox(bool isChecked) => new()
    {
        IsChecked = isChecked,
        IsEnabled = false,
        Width = 14,
        Height = 14,
        MinWidth = 0,
        MinHeight = 0,
        Margin = new Thickness(0, 3, 6, 0),
        VerticalAlignment = VerticalAlignment.Top,
        Template = new FuncControlTemplate<CheckBox>((_, _) => TaskBox(isChecked)),
    };

    private Panel TaskBox(bool isChecked)
    {
        var box = new Border { CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1) };
        var panel = new Panel { Children = { box } };
        if (isChecked)
        {
            var fill = _viewer.GetObservable(MarkdownViewer.LinkForegroundProperty);
            box.Bind(Border.BorderBrushProperty, fill);
            box.Bind(Border.BackgroundProperty, fill);
            panel.Children.Add(new global::Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(TaskGlyphs.CheckPath),
                Stroke = Brushes.White,
                StrokeThickness = 1.75,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
            });
        }
        else
        {
            box.Bind(Border.BorderBrushProperty, _viewer.GetObservable(MarkdownViewer.RuleBrushProperty));
        }
        return panel;
    }

    private Border Quote(QuoteNode quote, int depth)
    {
        var border = Expose(new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 0, 2),
            Child = Blocks(quote.Blocks, depth, spacing: 8),
        }, AutomationControlType.Group);
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

        var title = new DecorativeTextBlock
        {
            Text = AlertGlyphs.Title(alert.Kind),
            FontWeight = FontWeight.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.Bind(TextBlock.ForegroundProperty, brush);

        // The group is named by the title, so the title is not read a second time.
        var content = Blocks(alert.Blocks, depth, spacing: 8);
        content.Children.Insert(0, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { icon, title } });

        var border = Expose(new Border
        {
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(10, 2, 0, 2),
            Child = content,
        }, AutomationControlType.Group, AlertGlyphs.Title(alert.Kind));
        border.Bind(Border.BorderBrushProperty, brush);
        return border;
    }

    /// <summary>
    /// Blocks centered or right-aligned: the lines of text, and the images in them, align inside their block; any other
    /// block keeps its own width and moves across.
    /// </summary>
    private StackPanel Aligned(AlignedBlockNode aligned, int depth)
    {
        var panel = Blocks(aligned.Blocks, depth, spacing: 8);
        foreach (var child in panel.Children)
        {
            if (child is TextBlock text)
            {
                text.TextAlignment = aligned.Alignment == BlockAlignment.Right ? TextAlignment.Right : TextAlignment.Center;
            }
            else
            {
                child.HorizontalAlignment = aligned.Alignment == BlockAlignment.Right ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            }
        }
        return panel;
    }

    /// <summary>
    /// A section the reader opens and closes. The summary does not take the pointer, so a click anywhere on it toggles
    /// the section, as in a browser; a link in a summary therefore does not open. The viewer remembers what the reader
    /// did, so a section that is rebuilt while its text arrives stays as the reader left it.
    /// </summary>
    private Expander Details(DetailsNode details, int depth)
    {
        var summary = Text(details.Summary);
        summary.IsHitTestVisible = false;

        // The header is the tab stop, and the summary's text names it.
        summary.IsTabStop = false;
        summary.ExcludeLinksFromTabOrder();
        summary.Focusable = false;
        var key = _viewer.DetailsKey(details);
        var expander = new Expander
        {
            Header = summary,
            Content = Blocks(details.Blocks, depth, spacing: 8),
            IsExpanded = _viewer.IsDetailsOpen(key, details.IsOpen),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
        };
        AutomationProperties.SetName(expander, summary.AccessibleText);
        expander.PropertyChanged += (_, change) =>
        {
            if (change.Property == Expander.IsExpandedProperty)
            {
                _viewer.RememberDetails(key, expander.IsExpanded);
            }
        };
        return expander;
    }

    /// <summary>The marker before a list item: a check box for a task, otherwise its number or a bullet.</summary>
    private Control Marker(ListNode list, ListItemNode item, int index, int depth)
    {
        if (item.IsChecked is { } isChecked)
        {
            return TaskCheckBox(isChecked);
        }
        var text = list.IsOrdered
            ? (list.Start + index).ToString(System.Globalization.CultureInfo.InvariantCulture) + "."
            : s_bullets[depth % s_bullets.Length];

        // A bullet is drawn, not read, as a browser does not read the marker of an unordered list; a number is read.
        TextBlock marker = list.IsOrdered ? new TextBlock() : new DecorativeTextBlock();
        marker.Text = text;
        marker.Margin = new Thickness(0, 0, 6, 0);
        marker.HorizontalAlignment = HorizontalAlignment.Right;
        marker.MinWidth = list.IsOrdered ? 18 : 12;
        marker.TextAlignment = TextAlignment.Right;
        return marker;
    }

    private ScrollViewer Table(TableNode table)
    {
        _rendered.Add(typeof(TableRowNode));
        _rendered.Add(typeof(TableCellNode));

        var columns = table.Alignments.Count;
        var grid = Expose(new Grid(), AutomationControlType.Table);
        grid.SetValue(MarkdownViewer.TextGroupProperty, MarkdownViewer.TextGroup.Table);
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
                cell.Child!.SetValue(MarkdownViewer.TableRowProperty, rowIndex);

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

        // NOTE: Avalonia's macOS backend shows a header item as a button, which VoiceOver would offer to press, so there a
        // header cell is a cell like the others; its row comes first.
        var cell = Expose(
            new Border { Padding = new Thickness(8, 4), Child = text },
            header && !OperatingSystem.IsMacOS() ? AutomationControlType.HeaderItem : AutomationControlType.DataItem);
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
}
