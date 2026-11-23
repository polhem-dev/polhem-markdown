using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class HighlightingTests(HeadlessSession session)
{
    [Fact(DisplayName = "Highlighted code keeps its text, and each token takes its kind's brush")]
    public Task Render_HighlightedCode_ColorsTokens() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```cs\nvar s = \"x\"; // c\n```");
        var body = Descendants<SelectableTextBlock>(Assert.IsType<CodeBlockView>(Assert.Single(Blocks(viewer)))).Single();

        Assert.Equal("var s = \"x\"; // c", body.Inlines!.Text);
        var runs = body.Inlines.OfType<Run>().ToList();
        Assert.Same(viewer.CodeKeywordBrush, runs.Single(run => run.Text == "var").Foreground);
        Assert.Same(viewer.CodeStringBrush, runs.Single(run => run.Text == "\"x\"").Foreground);
        Assert.Same(viewer.CodeCommentBrush, runs.Single(run => run.Text == "// c").Foreground);
    });

    [Fact(DisplayName = "Changing a code brush recolors the shown code without rebuilding it")]
    public Task CodeBrush_Change_AppliesWithoutRebuild() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```cs\nvar x;\n```");
        var block = Blocks(viewer).Single();
        var brush = new SolidColorBrush(Colors.Orange);

        viewer.CodeKeywordBrush = brush;

        Assert.Same(block, Blocks(viewer).Single());
        var keyword = Descendants<SelectableTextBlock>(block).Single().Inlines!.OfType<Run>().Single(run => run.Text == "var");
        Assert.Same(brush, keyword.Foreground);
    });

    [Fact(DisplayName = "Code in a language the highlighter does not know stays one plain text")]
    public Task Render_UnknownLanguage_IsPlain() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("```rust\nfn main() {}\n```");
        var body = Descendants<SelectableTextBlock>(Assert.IsType<CodeBlockView>(Assert.Single(Blocks(viewer)))).Single();

        Assert.Equal("fn main() {}", body.Text);
    });
}
