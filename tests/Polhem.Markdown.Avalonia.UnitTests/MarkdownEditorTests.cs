using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit.Rendering;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class MarkdownEditorTests(HeadlessSession session)
{
    private static MarkdownEditor Show(string markdown)
    {
        var editor = new MarkdownEditor { Markdown = markdown };
        var window = new Window { Width = 600, Height = 400, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    private static void Type(MarkdownEditor editor, string text)
    {
        var area = editor.TextEditor.TextArea;
        foreach (var character in text)
        {
            area.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = character.ToString() });
        }
    }

    /// <summary>
    /// The colour of a brush. AvaloniaEdit draws with an immutable copy of the brush it is given, so brushes compare by
    /// colour.
    /// </summary>
    private static Color? ColorOf(IBrush? brush) => (brush as ISolidColorBrush)?.Color;

    /// <summary>The visual element of the line that holds <paramref name="offset"/>, as the editor draws it.</summary>
    private static VisualLineElement ElementAt(MarkdownEditor editor, int offset)
    {
        var textView = editor.TextEditor.TextArea.TextView;
        textView.EnsureVisualLines();
        var line = editor.TextEditor.Document.GetLineByOffset(offset);
        var visualLine = textView.GetOrConstructVisualLine(line);
        var column = offset - line.Offset;
        return visualLine.Elements.First(element =>
            column >= element.RelativeTextOffset && column < element.RelativeTextOffset + element.DocumentLength);
    }

    [Fact(DisplayName = "The editor draws its text under a host theme with no AvaloniaEdit styles")]
    public Task Show_HostThemeWithoutEditorStyles_DrawsText() => session.RunAsync(() =>
    {
        // NOTE: the test application uses Avalonia's Simple theme and includes no AvaloniaEdit theme, as a host on any
        // other theme would not; the editor must bring what its parts need.
        var editor = Show("# Title\n\ntext");

        Assert.True(editor.TextEditor.TextArea.TextView.Bounds.Height > 0);
        Assert.Equal("# Title\n\ntext", editor.TextEditor.Text);
    });

    [Fact(DisplayName = "The search panel opens under a host theme with no AvaloniaEdit styles")]
    public Task OpenSearch_HostThemeWithoutEditorStyles_Opens() => session.RunAsync(() =>
    {
        // NOTE: AvaloniaEdit's own Simple theme threw KeyNotFoundException here under a Semi host, for a font size
        // resource only Avalonia's Simple theme defines.
        var editor = Show("text");

        editor.TextEditor.SearchPanel.Open();
        Dispatcher.UIThread.RunJobs();

        Assert.True(editor.TextEditor.SearchPanel.IsOpened);
    });

    [Fact(DisplayName = "The editor's own resources follow the theme variant")]
    public Task Resources_FollowThemeVariant() => session.RunAsync(() =>
    {
        var editor = Show("text");

        Assert.True(editor.TryFindResource("SearchPanelBackgroundBrush", ThemeVariant.Light, out var light));
        Assert.True(editor.TryFindResource("SearchPanelBackgroundBrush", ThemeVariant.Dark, out var dark));
        Assert.NotEqual(ColorOf((IBrush?)light), ColorOf((IBrush?)dark));
    });

    [Fact(DisplayName = "Typing updates Markdown, which binds two-way by default")]
    public Task Type_UpdatesMarkdown() => session.RunAsync(() =>
    {
        var editor = Show("text");
        editor.CaretOffset = 4;

        Type(editor, "!");

        Assert.Equal("text!", editor.Markdown);
        Assert.Equal(BindingMode.TwoWay, MarkdownEditor.MarkdownProperty.GetMetadata(typeof(MarkdownEditor)).DefaultBindingMode);
    });

    [Fact(DisplayName = "Setting Markdown replaces the text and clears the undo history")]
    public Task SetMarkdown_ClearsUndo() => session.RunAsync(() =>
    {
        var editor = Show("a");
        editor.CaretOffset = 1;
        Type(editor, "b");

        editor.Markdown = "other";

        Assert.Equal("other", editor.TextEditor.Text);
        Assert.False(editor.Undo());
    });

    [Fact(DisplayName = "Undo and redo step through the edits and keep Markdown in step")]
    public Task UndoRedo_KeepMarkdownInStep() => session.RunAsync(() =>
    {
        var editor = Show("a");
        editor.CaretOffset = 1;
        Type(editor, "b");

        Assert.True(editor.Undo());
        Assert.Equal("a", editor.Markdown);
        Assert.True(editor.Redo());
        Assert.Equal("ab", editor.Markdown);
    });

    [Fact(DisplayName = "The source is coloured from the scan: a heading in the heading brush and bold, markers in theirs")]
    public Task Scan_ColoursHeadingAndMarkers() => session.RunAsync(async () =>
    {
        var editor = Show("# Title\n\na `code` b");
        await editor.ScanAsync();

        var heading = ElementAt(editor, 3);
        Assert.Equal(ColorOf(editor.HeadingBrush), ColorOf(heading.TextRunProperties.ForegroundBrush));
        Assert.Equal(FontWeight.Bold, heading.TextRunProperties.Typeface.Weight);
        Assert.Equal(ColorOf(editor.MarkerBrush), ColorOf(ElementAt(editor, 0).TextRunProperties.ForegroundBrush));
        Assert.Equal(ColorOf(editor.CodeBrush), ColorOf(ElementAt(editor, "# Title\n\na `c".Length - 1).TextRunProperties.ForegroundBrush));
    });

    [Fact(DisplayName = "Fenced code is coloured with the viewer's code brushes")]
    public Task Scan_ColoursFencedCode() => session.RunAsync(async () =>
    {
        var editor = Show("```cs\nvar x = \"s\";\n```");
        await editor.ScanAsync();

        Assert.Equal(ColorOf(editor.CodeKeywordBrush), ColorOf(ElementAt(editor, "```cs\n".Length).TextRunProperties.ForegroundBrush));
        Assert.Equal(ColorOf(editor.CodeStringBrush), ColorOf(ElementAt(editor, "```cs\nvar x = ".Length).TextRunProperties.ForegroundBrush));
    });

    [Fact(DisplayName = "Until the next scan the colours move with an edit")]
    public Task Type_BeforeScan_MovesColours() => session.RunAsync(async () =>
    {
        var editor = Show("a **b**");
        await editor.ScanAsync();
        editor.CaretOffset = 0;

        Type(editor, "xyz");

        Assert.Contains(editor.Colorizer.Spans, span => span.Range.Start == 5 && span.Kind == MarkdownSyntaxKind.Marker);
        Assert.Equal(FontWeight.Bold, ElementAt(editor, "xyza **".Length).TextRunProperties.Typeface.Weight);
    });

    [Fact(DisplayName = "Typing schedules a scan that colours the new text")]
    public Task Type_SchedulesScan() => session.RunAsync(async () =>
    {
        var editor = Show("");

        Type(editor, "# New");
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!editor.Colorizer.Spans.Any(span => span.Kind == MarkdownSyntaxKind.Heading) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.Contains(editor.Colorizer.Spans, span => span.Kind == MarkdownSyntaxKind.Heading);
    });

    [Fact(DisplayName = "The code brushes are the viewer's, with its defaults, so code looks the same in both")]
    public Task CodeBrushes_AreTheViewers() => session.RunAsync(() =>
    {
        var editor = new MarkdownEditor();
        var viewer = new MarkdownViewer();

        Assert.Same(MarkdownViewer.CodeKeywordBrushProperty, MarkdownEditor.CodeKeywordBrushProperty);
        Assert.Equal(ColorOf(viewer.CodeKeywordBrush), ColorOf(editor.CodeKeywordBrush));
        Assert.Equal(ColorOf(viewer.CodeStringBrush), ColorOf(editor.CodeStringBrush));
        Assert.Equal(ColorOf(viewer.CodeCommentBrush), ColorOf(editor.CodeCommentBrush));
        Assert.Equal(ColorOf(viewer.CodeConstantBrush), ColorOf(editor.CodeConstantBrush));
        Assert.Equal(ColorOf(viewer.CodeTagBrush), ColorOf(editor.CodeTagBrush));
        Assert.Equal(ColorOf(viewer.CodePropertyBrush), ColorOf(editor.CodePropertyBrush));
    });

    [Fact(DisplayName = "A new brush recolours the text")]
    public Task SetBrush_Recolours() => session.RunAsync(async () =>
    {
        var editor = Show("# Title");
        await editor.ScanAsync();
        var brush = new SolidColorBrush(Colors.Red);

        editor.HeadingBrush = brush;

        Assert.Equal(ColorOf(brush), ColorOf(ElementAt(editor, 3).TextRunProperties.ForegroundBrush));
    });

    [Fact(DisplayName = "Links in the source cannot be opened from the editor")]
    public Task Show_HyperlinksAreOff() => session.RunAsync(() =>
    {
        var editor = Show("https://example.com/");

        Assert.False(editor.TextEditor.Options.EnableHyperlinks);
        Assert.False(editor.TextEditor.Options.EnableEmailHyperlinks);
    });

    [Fact(DisplayName = "Read-only, line numbers, word wrap and the font reach the editor inside")]
    public Task Properties_ReachTheEditor() => session.RunAsync(() =>
    {
        var editor = Show("text");
        var font = new FontFamily("Menlo");

        editor.IsReadOnly = true;
        editor.ShowLineNumbers = false;
        editor.WordWrap = false;
        editor.CodeFontFamily = font;
        var selection = new SolidColorBrush(Colors.Orange);
        editor.SelectionBrush = selection;

        Assert.True(editor.TextEditor.IsReadOnly);
        Assert.False(editor.TextEditor.ShowLineNumbers);
        Assert.False(editor.TextEditor.WordWrap);
        Assert.Equal(font, editor.TextEditor.FontFamily);
        Assert.Same(selection, editor.TextEditor.TextArea.SelectionBrush);
    });
}
