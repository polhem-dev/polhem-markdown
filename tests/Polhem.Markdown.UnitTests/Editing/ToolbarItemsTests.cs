using Polhem.Markdown.Editing;

namespace Polhem.Markdown.UnitTests.Editing;

public class ToolbarItemsTests
{
    [Fact(DisplayName = "The toolbar shows every button once")]
    public void Groups_HoldEveryItemOnce()
    {
        var shown = ToolbarItems.Groups.SelectMany(group => group).ToList();

        Assert.Equal(Enum.GetValues<MarkdownToolbarItem>().Order(), shown.Order());
        Assert.All(ToolbarItems.Groups, group => Assert.NotEmpty(group));
    }

    [Fact(DisplayName = "A button named after a format applies that format; the heading, undo and redo buttons apply none")]
    public void Format_MatchesTheItem()
    {
        foreach (var item in Enum.GetValues<MarkdownToolbarItem>())
        {
            var format = ToolbarItems.Format(item);
            if (item is MarkdownToolbarItem.Heading or MarkdownToolbarItem.Undo or MarkdownToolbarItem.Redo)
            {
                Assert.Null(format);
            }
            else
            {
                Assert.Equal(item.ToString(), format?.ToString());
            }
        }
    }

    [Fact(DisplayName = "Every button has a label")]
    public void Label_IsSetForEveryItem()
    {
        Assert.All(Enum.GetValues<MarkdownToolbarItem>(), item => Assert.False(string.IsNullOrWhiteSpace(ToolbarItems.Label(item))));
    }

    [Theory(DisplayName = "A tooltip adds the shortcut the editors handle, written for the platform")]
    [InlineData(MarkdownToolbarItem.Bold, false, "Bold (Ctrl+B)")]
    [InlineData(MarkdownToolbarItem.Bold, true, "Bold (⌘B)")]
    [InlineData(MarkdownToolbarItem.Link, false, "Link (Ctrl+K)")]
    [InlineData(MarkdownToolbarItem.Redo, false, "Redo (Ctrl+Y)")]
    [InlineData(MarkdownToolbarItem.Redo, true, "Redo (⇧⌘Z)")]
    [InlineData(MarkdownToolbarItem.Table, false, "Table")]
    public void Tooltip_AddsShortcut(MarkdownToolbarItem item, bool mac, string expected)
    {
        Assert.Equal(expected, ToolbarItems.Tooltip(item, ToolbarItems.Label(item), mac));
    }
}
