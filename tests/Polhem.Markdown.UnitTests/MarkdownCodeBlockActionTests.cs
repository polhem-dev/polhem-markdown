namespace Polhem.Markdown.UnitTests;

public class MarkdownCodeBlockActionTests
{
    [Theory(DisplayName = "An action needs a label, since it is the button's text or its accessible name")]
    [InlineData("")]
    [InlineData("  ")]
    public void Constructor_BlankLabel_Throws(string label) =>
        Assert.Throws<ArgumentException>(() => new MarkdownCodeBlockAction(label, _ => { }));

    [Fact(DisplayName = "An action with no visibility test shows on every block")]
    public void IsShownOn_WithoutTest_IsTrue() =>
        Assert.True(new MarkdownCodeBlockAction("Insert", _ => { }).IsShownOn(new MarkdownCodeBlock("x", "")));

    [Fact(DisplayName = "An action whose work completes before it returns has run when its task is returned")]
    public void InvokeAsync_SyncAction_HasRun()
    {
        string? code = null;
        var task = new MarkdownCodeBlockAction("Insert", block => code = block.Code).InvokeAsync(new MarkdownCodeBlock("x", ""));

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal("x", code);
    }
}
