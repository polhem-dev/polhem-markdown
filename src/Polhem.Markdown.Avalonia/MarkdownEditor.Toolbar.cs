using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Avalonia;

// The toolbar above the text: a button for each format, the heading cycle, undo and redo, in the groups and order of
// ToolbarItems, which the Blazor editor's toolbar follows too.
public partial class MarkdownEditor
{
    /// <summary>Defines the <see cref="ShowToolbar"/> property.</summary>
    public static readonly StyledProperty<bool> ShowToolbarProperty =
        AvaloniaProperty.Register<MarkdownEditor, bool>(nameof(ShowToolbar), true);

    /// <summary>Defines the <see cref="ToolbarLabels"/> property.</summary>
    public static readonly StyledProperty<Func<MarkdownToolbarItem, string?>?> ToolbarLabelsProperty =
        AvaloniaProperty.Register<MarkdownEditor, Func<MarkdownToolbarItem, string?>?>(nameof(ToolbarLabels));

    /// <summary>Defines the <see cref="ToolbarBackground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ToolbarBackgroundProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush?>(nameof(ToolbarBackground));

    /// <summary>Defines the <see cref="ToolbarBorderBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> ToolbarBorderBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(ToolbarBorderBrush), new SolidColorBrush(Color.FromArgb(0x59, 0x8A, 0x8F, 0x98)));

    private readonly Border _toolbar;
    private readonly Dictionary<MarkdownToolbarItem, Button> _toolbarButtons = [];

    /// <summary>Gets or sets whether the toolbar shows above the text. The default is <see langword="true"/>.</summary>
    /// <remarks>
    /// The toolbar has a button for each <see cref="MarkdownToolbarItem"/>. A host that lays out buttons of its own sets
    /// this to <see langword="false"/> and calls <see cref="ApplyFormat"/>, <see cref="CycleHeading"/>,
    /// <see cref="Undo"/> and <see cref="Redo"/> from them.
    /// </remarks>
    public bool ShowToolbar
    {
        get => GetValue(ShowToolbarProperty);
        set => SetValue(ShowToolbarProperty, value);
    }

    /// <summary>
    /// Gets or sets the labels of the toolbar's buttons: a function that returns the label of a button, or
    /// <see langword="null"/> to keep the English one. The default, <see langword="null"/>, keeps all the English labels.
    /// </summary>
    /// <remarks>
    /// A label is the button's tooltip and its name for screen readers. The tooltip adds the button's shortcut, written
    /// for the platform, so a label does not need to. The labels are read when this property is set; to show labels of
    /// another language, set it again.
    /// </remarks>
    public Func<MarkdownToolbarItem, string?>? ToolbarLabels
    {
        get => GetValue(ToolbarLabelsProperty);
        set => SetValue(ToolbarLabelsProperty, value);
    }

    /// <summary>Gets or sets the background of the toolbar. The default, <see langword="null"/>, lets the host's background show.</summary>
    public IBrush? ToolbarBackground
    {
        get => GetValue(ToolbarBackgroundProperty);
        set => SetValue(ToolbarBackgroundProperty, value);
    }

    /// <summary>Gets or sets the brush of the line under the toolbar and of the separators between its groups of buttons.</summary>
    public IBrush ToolbarBorderBrush
    {
        get => GetValue(ToolbarBorderBrushProperty);
        set => SetValue(ToolbarBorderBrushProperty, value);
    }

    /// <summary>The toolbar, for the tests.</summary>
    internal Control Toolbar => _toolbar;

    /// <summary>The button of <paramref name="item"/>, for the tests.</summary>
    internal Button ToolbarButton(MarkdownToolbarItem item) => _toolbarButtons[item];

    /// <summary>
    /// Builds the toolbar. Tab reaches it once, on the button last used, and the arrow keys move between its buttons.
    /// </summary>
    private Border CreateToolbar()
    {
        var panel = new WrapPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(4, 2) };
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Once);
        AutomationProperties.SetName(panel, "Formatting");
        foreach (var group in ToolbarItems.Groups)
        {
            if (panel.Children.Count > 0)
            {
                var separator = new Border { Width = 1, Height = 16, Margin = new Thickness(4, 0), VerticalAlignment = VerticalAlignment.Center };
                separator.Bind(Border.BackgroundProperty, this.GetObservable(ToolbarBorderBrushProperty));
                panel.Children.Add(separator);
            }
            foreach (var item in group)
            {
                var button = CreateToolbarButton(item);
                _toolbarButtons[item] = button;
                panel.Children.Add(button);
            }
        }
        panel.AddHandler(KeyDownEvent, OnToolbarKeyDown);

        var toolbar = new Border { Child = panel, BorderThickness = new Thickness(0, 0, 0, 1) };
        toolbar.Bind(Border.BackgroundProperty, this.GetObservable(ToolbarBackgroundProperty));
        toolbar.Bind(Border.BorderBrushProperty, this.GetObservable(ToolbarBorderBrushProperty));

        _editor.Document.UndoStack.PropertyChanged += OnUndoStackChanged;
        UpdateToolbarLabels();
        UpdateToolbarState();
        return toolbar;
    }

    private Button CreateToolbarButton(MarkdownToolbarItem item)
    {
        var icon = new global::Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse(ToolbarItems.Icon(item)),
            Width = 16,
            Height = 16,
            StrokeThickness = 1.5,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round,
        };

        // The icon takes the colour the host's theme gives the button's text, so it follows hover, press and disabled.
        icon.Bind(Shape.StrokeProperty, icon.GetObservable(TextElement.ForegroundProperty));

        var button = new Button
        {
            Content = icon,
            Padding = new Thickness(5),
            MinWidth = 0,
            MinHeight = 0,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        button.Click += (_, _) =>
        {
            RunToolbarItem(item);
            _editor.TextArea.Focus();
        };
        return button;
    }

    private void RunToolbarItem(MarkdownToolbarItem item)
    {
        if (ToolbarItems.Format(item) is { } format)
        {
            ApplyFormat(format);
            return;
        }
        switch (item)
        {
            case MarkdownToolbarItem.Heading:
                CycleHeading();
                break;
            case MarkdownToolbarItem.Undo:
                Undo();
                break;
            case MarkdownToolbarItem.Redo:
                Redo();
                break;
        }
    }

    private void UpdateToolbarLabels()
    {
        var mac = CommandModifiers() == KeyModifiers.Meta;
        foreach (var (item, button) in _toolbarButtons)
        {
            var label = ToolbarLabels?.Invoke(item) ?? ToolbarItems.Label(item);
            ToolTip.SetTip(button, ToolbarItems.Tooltip(item, label, mac));
            AutomationProperties.SetName(button, label);
        }
    }

    /// <summary>Enables the buttons that can do something: none while read-only, and undo and redo only with a step to take.</summary>
    private void UpdateToolbarState()
    {
        var undoStack = _editor.Document.UndoStack;
        foreach (var (item, button) in _toolbarButtons)
        {
            button.IsEnabled = !IsReadOnly && item switch
            {
                MarkdownToolbarItem.Undo => undoStack.CanUndo,
                MarkdownToolbarItem.Redo => undoStack.CanRedo,
                _ => true,
            };
        }
    }

    private void OnUndoStackChanged(object? sender, PropertyChangedEventArgs e) => UpdateToolbarState();

    /// <summary>Moves between the toolbar's buttons with the arrow keys, Home and End, and back to the text with Escape.</summary>
    private void OnToolbarKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _editor.TextArea.Focus();
            e.Handled = true;
            return;
        }
        var buttons = ((Panel)sender!).Children.OfType<Button>().Where(button => button.IsEnabled).ToList();
        var current = buttons.FindIndex(button => button.IsFocused);
        if (current < 0 || buttons.Count == 0)
        {
            return;
        }
        var next = e.Key switch
        {
            Key.Left => (current + buttons.Count - 1) % buttons.Count,
            Key.Right => (current + 1) % buttons.Count,
            Key.Home => 0,
            Key.End => buttons.Count - 1,
            _ => -1,
        };
        if (next >= 0)
        {
            buttons[next].Focus(NavigationMethod.Directional);
            e.Handled = true;
        }
    }
}
