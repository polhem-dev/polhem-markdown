using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Threading;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class CjkFontFamilyTests(HeadlessSession session)
{
    private static readonly FontFamily s_cjk = new("PingFang TC, Microsoft JhengHei UI, Noto Sans CJK TC");

    [Fact(DisplayName = "Without a CJK font family, no run of the text sets a font family of its own")]
    public Task Render_NoCjkFontFamily_LeavesRunsAlone() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("這是**京都三日遊**的安排");

        var text = Paragraph(viewer);
        Assert.Equal("這是京都三日遊的安排", text.Inlines!.Text);
        Assert.All(Runs(text), run => Assert.False(run.IsSet(TextElement.FontFamilyProperty)));
    });

    [Fact(DisplayName = "With a CJK font family, CJK text is drawn in it and other text keeps its font")]
    public Task Render_CjkFontFamily_DrawsCjkTextInIt() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("Plan: 這是**京都 Kyoto 三日遊**的安排 ok");
        viewer.CjkFontFamily = s_cjk;
        Dispatcher.UIThread.RunJobs();

        var text = Paragraph(viewer);
        Assert.Equal("Plan: 這是京都 Kyoto 三日遊的安排 ok", text.Inlines!.Text);
        var runs = Runs(text).Where(run => run.Text!.Length > 0).ToList();
        Assert.Equal(["Plan: ", "這是", "京都", " Kyoto ", "三日遊", "的安排", " ok"], runs.Select(run => run.Text));
        Assert.All(runs, run => Assert.Equal(
            run.Text!.EnumerateRunes().Any(CjkText.IsCjk), run.IsSet(TextElement.FontFamilyProperty)));
        Assert.All(runs.Where(run => run.IsSet(TextElement.FontFamilyProperty)), run => Assert.Same(s_cjk, run.FontFamily));
        Assert.Equal(FontWeight.Bold, runs.Single(run => run.Text == "京都").FontWeight);
        Assert.Equal(FontWeight.Normal, runs.Single(run => run.Text == "的安排").FontWeight);
    });

    [Fact(DisplayName = "Inline code keeps the code font family when a CJK font family is set")]
    public Task Render_CjkFontFamily_KeepsCodeFont() => session.RunAsync(() =>
    {
        var viewer = new MarkdownViewer { ImageLoader = NoImages, CjkFontFamily = s_cjk, Markdown = "看 `程式碼` 吧" };
        new Window { Content = viewer }.Show();
        Dispatcher.UIThread.RunJobs();

        var code = Runs(Paragraph(viewer)).Single(run => run.Text == "程式碼");
        Assert.Same(viewer.CodeFontFamily, code.FontFamily);
    });

    [Fact(DisplayName = "A link over CJK text keeps the range of its text when a CJK font family is set")]
    public Task Render_CjkFontFamily_KeepsLinkRange() => session.RunAsync(() =>
    {
        var viewer = new MarkdownViewer { ImageLoader = NoImages, CjkFontFamily = s_cjk, Markdown = "去 [京都 Kyoto](https://example.com) 玩" };
        new Window { Content = viewer }.Show();
        Dispatcher.UIThread.RunJobs();

        var text = Assert.IsType<MarkdownTextBlock>(Paragraph(viewer));
        var link = Assert.Single(text.Links);
        Assert.Equal("京都 Kyoto", text.Inlines!.Text!.Substring(link.Start, link.Length));
    });

    [Fact(DisplayName = "Clearing the CJK font family draws the text as before")]
    public Task Render_CjkFontFamilyCleared_RestoresRuns() => session.RunAsync(() =>
    {
        var viewer = new MarkdownViewer { ImageLoader = NoImages, CjkFontFamily = s_cjk, Markdown = "這是**京都**" };
        new Window { Content = viewer }.Show();
        Dispatcher.UIThread.RunJobs();
        viewer.CjkFontFamily = null;
        Dispatcher.UIThread.RunJobs();

        Assert.All(Runs(Paragraph(viewer)), run => Assert.False(run.IsSet(TextElement.FontFamilyProperty)));
    });

    [Theory(DisplayName = "Text splits into CJK and other stretches that cover it without splitting a surrogate pair")]
    [InlineData("Kyoto", "Kyoto|")]
    [InlineData("京都", "京都*")]
    [InlineData("Plan: 京都，ok", "Plan: |京都，*ok|")]
    [InlineData("한국어 かな ㄅㄆ", "한국어* |かな* |ㄅㄆ*")]
    [InlineData("𠀀 and 😀", "𠀀* and 😀|")]
    public void Split_MixedText_CoversItInStretches(string text, string expected)
    {
        var stretches = CjkText.Split(text);

        var described = new StringBuilder();
        foreach (var (start, length, isCjk) in stretches)
        {
            described.Append(text, start, length).Append(isCjk ? '*' : '|');
        }
        Assert.Equal(expected, described.ToString());
    }

    [Fact(DisplayName = "Loading the heavier faces skips a family that is not installed")]
    public Task LoadHeavierFaces_FamilyNotInstalled_DoesNothing() => session.RunAsync(() =>
        FontFaceWarmUp.LoadHeavierFaces(new FontFamily("No Such Font Polhem, Another Missing Font")));

    [Fact(DisplayName = "Empty text has no stretches")]
    public void Split_Empty_HasNoStretches() => Assert.Empty(CjkText.Split(""));

    private static TextBlock Paragraph(MarkdownViewer viewer) =>
        Assert.IsType<TextBlock>(Assert.Single(Blocks(viewer)), exactMatch: false);

    private static IEnumerable<Run> Runs(TextBlock text) => Flatten(text.Inlines!);

    private static IEnumerable<Run> Flatten(InlineCollection inlines)
    {
        foreach (var inline in inlines)
        {
            if (inline is Run run)
            {
                yield return run;
            }
            else if (inline is Span span)
            {
                foreach (var inner in Flatten(span.Inlines))
                {
                    yield return inner;
                }
            }
        }
    }
}
