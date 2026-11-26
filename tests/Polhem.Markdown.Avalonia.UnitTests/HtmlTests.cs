using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Polhem.Markdown.Avalonia.Images;
using static Polhem.Markdown.Avalonia.UnitTests.TestImages;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>The elements of raw HTML the model takes over, as the Avalonia viewer draws them.</summary>
[Collection(HeadlessCollection.Name)]
public class HtmlTests(HeadlessSession session)
{
    private const string Picture = """
        <picture>
          <source media="(prefers-color-scheme: dark)" srcset="https://example.com/dark.png">
          <img alt="Logo" src="https://example.com/light.png">
        </picture>
        """;

    [Fact(DisplayName = "A centered block centers its lines of text, and a right-aligned one aligns them right")]
    public Task Render_AlignedBlock_AlignsText() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("<p align=\"center\">centered</p>\n\n<div align=\"right\">\n\n- item\n\n</div>");

        var centered = Assert.IsType<StackPanel>(Blocks(viewer).First());
        Assert.Equal(TextAlignment.Center, Assert.IsType<MarkdownTextBlock>(Assert.Single(centered.Children)).TextAlignment);
        var right = Assert.IsType<StackPanel>(Blocks(viewer).Last());
        Assert.Equal(HorizontalAlignment.Right, Assert.IsType<StackPanel>(Assert.Single(right.Children)).HorizontalAlignment);
    });

    [Fact(DisplayName = "A details section is an expander with its summary, closed unless the text opens it")]
    public Task Render_Details_IsExpander() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("<details><summary>More <b>info</b></summary>\n\nHidden *text*.\n\n</details>\n\n<details open>\n\nOpen\n\n</details>");

        var expanders = Blocks(viewer).Cast<Expander>().ToList();
        Assert.False(expanders[0].IsExpanded);
        Assert.Equal("More info", Assert.IsType<MarkdownTextBlock>(expanders[0].Header).Inlines!.Text);
        var content = Assert.IsType<StackPanel>(expanders[0].Content);
        Assert.Equal("Hidden text.", Assert.IsType<MarkdownTextBlock>(Assert.Single(content.Children)).Inlines!.Text);
        Assert.True(expanders[1].IsExpanded);
        Assert.Equal("Details", Assert.IsType<MarkdownTextBlock>(expanders[1].Header).Inlines!.Text);
    });

    [Fact(DisplayName = "A section the reader opened stays open while its text arrives and text is added above it, and one with another summary starts as written")]
    public Task Render_DetailsOpened_StaysOpenWhileStreaming() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("<details><summary>Thinking</summary>\n\nStep one");
        Assert.IsType<Expander>(Assert.Single(Blocks(viewer))).IsExpanded = true;

        viewer.Markdown = "<details><summary>Thinking</summary>\n\nStep one and two";
        Dispatcher.UIThread.RunJobs();
        Assert.True(Assert.IsType<Expander>(Assert.Single(Blocks(viewer))).IsExpanded);

        viewer.Markdown = "Text above\n\n<details><summary>Thinking</summary>\n\nStep one and two";
        Dispatcher.UIThread.RunJobs();
        Assert.True(Assert.IsType<Expander>(Blocks(viewer).Last()).IsExpanded);

        viewer.Markdown = "<details><summary>Answer</summary>\n\nStep";
        Dispatcher.UIThread.RunJobs();
        Assert.False(Assert.IsType<Expander>(Assert.Single(Blocks(viewer))).IsExpanded);
    });

    [Fact(DisplayName = "A section whose place among sections of its summary changes takes the choice made for its new place")]
    public Task Render_DetailsRenumbered_FollowsItsKey() => session.RunAsync(() =>
    {
        const string Second = "\n\n<details><summary>X</summary>\n\ntwo\n\n</details>";
        var (_, viewer) = Show("<details><summary>X</summary>\n\none\n\n</details>" + Second);

        viewer.Markdown = "<details><summary>Y</summary>\n\none\n\n</details>" + Second;
        Dispatcher.UIThread.RunJobs();
        Assert.IsType<Expander>(Blocks(viewer).Last()).IsExpanded = true;

        viewer.Markdown += "\n\n<details><summary>X</summary>\n\nthree\n\n</details>";
        Dispatcher.UIThread.RunJobs();

        var expanders = Blocks(viewer).Cast<Expander>().ToList();
        Assert.True(expanders[1].IsExpanded);
        Assert.False(expanders[2].IsExpanded);
    });

    [Fact(DisplayName = "Superscript and subscript sit above and below the line, smaller; a key uses the code font")]
    public Task Render_SupSubKbd_AreStyled() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("x<sup>2</sup> H<sub>2</sub>O <kbd>Ctrl</kbd>");

        var spans = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer))).Inlines!.OfType<Span>().ToList();
        Assert.Equal(BaselineAlignment.Superscript, spans[0].BaselineAlignment);
        Assert.True(spans[0].FontSize < viewer.GetValue(TextElement.FontSizeProperty));
        Assert.Equal(BaselineAlignment.Subscript, spans[1].BaselineAlignment);
        Assert.Same(viewer.CodeFontFamily, spans[2].FontFamily);
        Assert.Same(viewer.CodeBackground, spans[2].Background);
    });

    [Fact(DisplayName = "An image with a width written in HTML is scaled to it, up as well as down")]
    public Task Render_ImageWidth_ScalesToIt() => session.RunAsync(async () =>
    {
        var (_, viewer) = Show("<img src=\"https://example.com/a.png\" width=\"200\">", images: Serve(Png(40, 20)));

        var view = Descendants<ImageView>(viewer).Single();
        await view.Shown.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
        Dispatcher.UIThread.RunJobs();

        var image = Assert.IsType<Image>(view.LoadedImage);
        Assert.Equal(StretchDirection.Both, image.StretchDirection);
        Assert.Equal(200, image.MaxWidth);
        Assert.Equal(200, image.Bounds.Width, precision: 0);
    });

    [Fact(DisplayName = "A picture loads the source for the viewer's theme, and the other one when the theme changes")]
    public Task Render_Picture_FollowsTheme() => session.RunAsync(async () =>
    {
        var calls = new List<Uri>();
        var (window, viewer) = Show(Picture, images: Serve(Png(10, 10), calls));
        window.RequestedThemeVariant = ThemeVariant.Light;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(new Uri("https://example.com/light.png"), Descendants<ImageView>(viewer).Single().Source);

        window.RequestedThemeVariant = ThemeVariant.Dark;
        Dispatcher.UIThread.RunJobs();

        // NOTE: the image loads on a background thread, so the loader is called only once the new view's load has run.
        var view = Descendants<ImageView>(viewer).Single();
        Assert.Equal(new Uri("https://example.com/dark.png"), view.Source);
        await view.Shown.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext);
        lock (calls)
        {
            Assert.Contains(new Uri("https://example.com/dark.png"), calls);
        }
    });
}
