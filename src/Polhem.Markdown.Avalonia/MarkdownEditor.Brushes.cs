using Avalonia;
using Avalonia.Media;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Avalonia;

// The brushes the source is coloured with. The code brushes have the viewer's names and defaults, so code looks the same
// in both.
public partial class MarkdownEditor
{
    /// <summary>Defines the <see cref="HeadingBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> HeadingBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(HeadingBrush), new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)));

    /// <summary>Defines the <see cref="MarkerBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> MarkerBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(MarkerBrush), new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98)));

    /// <summary>Defines the <see cref="LinkForeground"/> property.</summary>
    public static readonly StyledProperty<IBrush> LinkForegroundProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(LinkForeground), new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)));

    /// <summary>Defines the <see cref="UrlBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> UrlBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(UrlBrush), new SolidColorBrush(Color.FromRgb(0x22, 0xA3, 0x5A)));

    /// <summary>Defines the <see cref="CodeBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(CodeBrush), new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)));

    /// <summary>Defines the <see cref="MathBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> MathBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(MathBrush), new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)));

    /// <summary>Defines the <see cref="CodeKeywordBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeKeywordBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(CodeKeywordBrush), new SolidColorBrush(Color.FromRgb(0x8B, 0x5C, 0xF6)));

    /// <summary>Defines the <see cref="CodeStringBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeStringBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(CodeStringBrush), new SolidColorBrush(Color.FromRgb(0x22, 0xA3, 0x5A)));

    /// <summary>Defines the <see cref="CodeCommentBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeCommentBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(CodeCommentBrush), new SolidColorBrush(Color.FromRgb(0x8A, 0x8F, 0x98)));

    /// <summary>Defines the <see cref="CodeConstantBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeConstantBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(CodeConstantBrush), new SolidColorBrush(Color.FromRgb(0xD9, 0x77, 0x06)));

    /// <summary>Defines the <see cref="CodeTagBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodeTagBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(CodeTagBrush), new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)));

    /// <summary>Defines the <see cref="CodePropertyBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush> CodePropertyBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush>(nameof(CodePropertyBrush), new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6)));

    /// <summary>Defines the <see cref="SelectionBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty =
        AvaloniaProperty.Register<MarkdownEditor, IBrush?>(nameof(SelectionBrush));

    /// <summary>Gets or sets the brush of the text of a heading, which is also bold.</summary>
    public IBrush HeadingBrush
    {
        get => GetValue(HeadingBrushProperty);
        set => SetValue(HeadingBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of syntax marks: heading and emphasis markers, code fences, list bullets, quote markers, table pipes, link brackets and raw HTML.</summary>
    public IBrush MarkerBrush
    {
        get => GetValue(MarkerBrushProperty);
        set => SetValue(MarkerBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of the text of a link or an image, and footnote references.</summary>
    public IBrush LinkForeground
    {
        get => GetValue(LinkForegroundProperty);
        set => SetValue(LinkForegroundProperty, value);
    }

    /// <summary>Gets or sets the brush of the destination and title of a link or an image, and bare URLs.</summary>
    public IBrush UrlBrush
    {
        get => GetValue(UrlBrushProperty);
        set => SetValue(UrlBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of inline code, and code in a block the highlighter does not colour.</summary>
    public IBrush CodeBrush
    {
        get => GetValue(CodeBrushProperty);
        set => SetValue(CodeBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of formulas.</summary>
    public IBrush MathBrush
    {
        get => GetValue(MathBrushProperty);
        set => SetValue(MathBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of keywords in highlighted code.</summary>
    public IBrush CodeKeywordBrush
    {
        get => GetValue(CodeKeywordBrushProperty);
        set => SetValue(CodeKeywordBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of strings in highlighted code.</summary>
    public IBrush CodeStringBrush
    {
        get => GetValue(CodeStringBrushProperty);
        set => SetValue(CodeStringBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of comments in highlighted code.</summary>
    public IBrush CodeCommentBrush
    {
        get => GetValue(CodeCommentBrushProperty);
        set => SetValue(CodeCommentBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of numbers and constants such as <c>true</c> and <c>null</c> in highlighted code.</summary>
    public IBrush CodeConstantBrush
    {
        get => GetValue(CodeConstantBrushProperty);
        set => SetValue(CodeConstantBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of markup tag names in highlighted code and raw HTML.</summary>
    public IBrush CodeTagBrush
    {
        get => GetValue(CodeTagBrushProperty);
        set => SetValue(CodeTagBrushProperty, value);
    }

    /// <summary>Gets or sets the brush of attribute names, JSON and YAML keys, CSS properties and shell variables in highlighted code and raw HTML.</summary>
    public IBrush CodePropertyBrush
    {
        get => GetValue(CodePropertyBrushProperty);
        set => SetValue(CodePropertyBrushProperty, value);
    }

    /// <summary>
    /// Gets or sets the brush of the selection. <see langword="null"/>, the default, uses a translucent blue that works on
    /// light and dark themes.
    /// </summary>
    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    /// <summary>The brush of a kind of span, or <see langword="null"/> to keep the text's own colour.</summary>
    internal IBrush? BrushFor(MarkdownSyntaxKind kind) => kind switch
    {
        MarkdownSyntaxKind.Marker or MarkdownSyntaxKind.Html => MarkerBrush,
        MarkdownSyntaxKind.Heading => HeadingBrush,
        MarkdownSyntaxKind.Code => CodeBrush,
        MarkdownSyntaxKind.Link => LinkForeground,
        MarkdownSyntaxKind.Url => UrlBrush,
        MarkdownSyntaxKind.Math => MathBrush,
        MarkdownSyntaxKind.CodeKeyword => CodeKeywordBrush,
        MarkdownSyntaxKind.CodeString => CodeStringBrush,
        MarkdownSyntaxKind.CodeComment => CodeCommentBrush,
        MarkdownSyntaxKind.CodeConstant => CodeConstantBrush,
        MarkdownSyntaxKind.CodeTag => CodeTagBrush,
        MarkdownSyntaxKind.CodeProperty => CodePropertyBrush,
        _ => null,
    };

    private static bool IsBrush(AvaloniaProperty property) =>
        property == HeadingBrushProperty
        || property == MarkerBrushProperty
        || property == LinkForegroundProperty
        || property == UrlBrushProperty
        || property == CodeBrushProperty
        || property == MathBrushProperty
        || property == CodeKeywordBrushProperty
        || property == CodeStringBrushProperty
        || property == CodeCommentBrushProperty
        || property == CodeConstantBrushProperty
        || property == CodeTagBrushProperty
        || property == CodePropertyBrushProperty;

    private void ApplySelectionBrush()
    {
        if (SelectionBrush is { } brush)
        {
            _editor.TextArea.SelectionBrush = brush;
        }
        else
        {
            _editor.TextArea.ClearValue(AvaloniaEdit.Editing.TextArea.SelectionBrushProperty);
        }
    }
}
