using Polhem.Markdown.Editing;

namespace Polhem.Markdown.UnitTests.Editing;

/// <summary>
/// The texts here mark the selection with <c>«»</c> and a caret with <c>‸</c>, before and after the edit.
/// </summary>
public class MarkdownFormatterTests
{
    private static (string Text, int Start, int Length) Parse(string marked)
    {
        var caret = marked.IndexOf('‸', StringComparison.Ordinal);
        if (caret >= 0)
        {
            return (marked.Remove(caret, 1), caret, 0);
        }
        var start = marked.IndexOf('«', StringComparison.Ordinal);
        var end = marked.IndexOf('»', StringComparison.Ordinal);
        return (marked.Remove(end, 1).Remove(start, 1), start, end - start - 1);
    }

    private static string Show(string text, MarkdownEdit? edit)
    {
        Assert.NotNull(edit);
        var (start, length, replacement, selectionStart, selectionLength) = edit.Value;
        var result = text[..start] + replacement + text[(start + length)..];
        return selectionLength == 0
            ? result.Insert(selectionStart, "‸")
            : result.Insert(selectionStart + selectionLength, "»").Insert(selectionStart, "«");
    }

    private static string Apply(string marked, MarkdownFormat format)
    {
        var (text, start, length) = Parse(marked);
        return Show(text, MarkdownFormatter.Apply(text, start, length, format));
    }

    [Theory(DisplayName = "An inline format wraps the selection, and unwraps it with the markers inside or outside")]
    [InlineData("a «word» b", MarkdownFormat.Bold, "a **«word»** b")]
    [InlineData("a **«word»** b", MarkdownFormat.Bold, "a «word» b")]
    [InlineData("a «**word**» b", MarkdownFormat.Bold, "a «word» b")]
    [InlineData("a «word» b", MarkdownFormat.Italic, "a *«word»* b")]
    [InlineData("a *«word»* b", MarkdownFormat.Italic, "a «word» b")]
    [InlineData("a «word» b", MarkdownFormat.Strikethrough, "a ~~«word»~~ b")]
    [InlineData("a ~~«word»~~ b", MarkdownFormat.Strikethrough, "a «word» b")]
    [InlineData("a «x» b", MarkdownFormat.InlineCode, "a `«x»` b")]
    [InlineData("a `«x»` b", MarkdownFormat.InlineCode, "a «x» b")]
    public void Apply_InlineFormat_Toggles(string before, MarkdownFormat format, string after)
    {
        Assert.Equal(after, Apply(before, format));
    }

    [Theory(DisplayName = "Bold and italic tell their asterisks apart")]
    [InlineData("**«word»**", MarkdownFormat.Italic, "***«word»***")]
    [InlineData("***«word»***", MarkdownFormat.Italic, "**«word»**")]
    [InlineData("***«word»***", MarkdownFormat.Bold, "*«word»*")]
    [InlineData("*«word»*", MarkdownFormat.Bold, "***«word»***")]
    public void Apply_BoldAndItalic_TellAsterisksApart(string before, MarkdownFormat format, string after)
    {
        Assert.Equal(after, Apply(before, format));
    }

    [Theory(DisplayName = "With nothing selected an inline format inserts a pair around the caret, or removes an empty one")]
    [InlineData("a ‸b", MarkdownFormat.Bold, "a **‸**b")]
    [InlineData("a **‸**b", MarkdownFormat.Bold, "a ‸b")]
    [InlineData("‸", MarkdownFormat.InlineCode, "`‸`")]
    public void Apply_InlineFormatAtCaret(string before, MarkdownFormat format, string after)
    {
        Assert.Equal(after, Apply(before, format));
    }

    [Fact(DisplayName = "The markers go around the selected words, not the spaces next to them")]
    public void Apply_InlineFormat_LeavesSpacesOutside()
    {
        Assert.Equal("a **«word»** b", Apply("a« word »b", MarkdownFormat.Bold));
    }

    [Theory(DisplayName = "A link wraps the selection and selects the address to type over")]
    [InlineData("see «docs» now", "see [docs](«url») now")]
    [InlineData("see ‸ now", "see [‸](url) now")]
    public void Apply_Link(string before, string after)
    {
        Assert.Equal(after, Apply(before, MarkdownFormat.Link));
    }

    [Theory(DisplayName = "A heading applies to the caret's line, replaces another level, and is removed at its own level")]
    [InlineData("ti‸tle", MarkdownFormat.Heading1, "# ti‸tle")]
    [InlineData("## ti‸tle", MarkdownFormat.Heading1, "# ti‸tle")]
    [InlineData("## ti‸tle", MarkdownFormat.Heading2, "ti‸tle")]
    [InlineData("‸title", MarkdownFormat.Heading3, "### ‸title")]
    public void Apply_Heading(string before, MarkdownFormat format, string after)
    {
        Assert.Equal(after, Apply(before, format));
    }

    [Theory(DisplayName = "A list applies to every selected line with text, replaces another kind, and is removed when all have it")]
    [InlineData("«a\nb»", MarkdownFormat.BulletList, "«- a\n- b»")]
    [InlineData("«- a\n- b»", MarkdownFormat.BulletList, "«a\nb»")]
    [InlineData("«a\n\nb»", MarkdownFormat.NumberedList, "«1. a\n\n2. b»")]
    [InlineData("«- a\n- b»", MarkdownFormat.NumberedList, "«1. a\n2. b»")]
    [InlineData("«1. a\n2. b»", MarkdownFormat.NumberedList, "«a\nb»")]
    [InlineData("«a\n- b»", MarkdownFormat.TaskList, "«- [ ] a\n- [ ] b»")]
    [InlineData("«- [ ] a\n- [x] b»", MarkdownFormat.TaskList, "«a\nb»")]
    [InlineData("«- [ ] a»", MarkdownFormat.BulletList, "«- a»")]
    [InlineData("«  - a\n  - b»", MarkdownFormat.NumberedList, "«  1. a\n  2. b»")]
    public void Apply_Lists(string before, MarkdownFormat format, string after)
    {
        Assert.Equal(after, Apply(before, format));
    }

    [Fact(DisplayName = "A line format reaches the whole lines a partial selection touches, but not a line it only ends at")]
    public void Apply_LineFormat_WholeLines()
    {
        Assert.Equal("«- one\n- two»\nthree", Apply("o«ne\ntw»o\nthree", MarkdownFormat.BulletList));
        Assert.Equal("«- one»\ntwo", Apply("«one\n»two", MarkdownFormat.BulletList));
    }

    [Theory(DisplayName = "A quote marks every selected line, blank ones too, and is removed when all are quoted")]
    [InlineData("«a\n\nb»", "«> a\n>\n> b»")]
    [InlineData("«> a\n>\n> b»", "«a\n\nb»")]
    public void Apply_Quote(string before, string after)
    {
        Assert.Equal(after, Apply(before, MarkdownFormat.Quote));
    }

    [Theory(DisplayName = "A code block fences the selected lines, or removes the fences around them")]
    [InlineData("«var x;\nvar y;»", "```\n«var x;\nvar y;»\n```")]
    [InlineData("«```\nvar x;\n```»", "«var x;»")]
    [InlineData("‸", "```\n‸\n```")]
    public void Apply_CodeBlock(string before, string after)
    {
        Assert.Equal(after, Apply(before, MarkdownFormat.CodeBlock));
    }

    [Fact(DisplayName = "Lines ending in CRLF keep their endings, and new lines take the text's ending")]
    public void Apply_CrLf_KeepsEndings()
    {
        Assert.Equal("«- a\r\n- b»\r\nc", Apply("«a\r\nb»\r\nc", MarkdownFormat.BulletList));
        Assert.Equal("```\r\n«x»\r\n```\r\nnext", Apply("«x»\r\nnext", MarkdownFormat.CodeBlock));
        Assert.Equal("first\r\n```\r\n«x»\r\n```", Apply("first\r\n«x»", MarkdownFormat.CodeBlock));
    }

    [Theory(DisplayName = "Enter on a list item starts the next one, numbered on, unchecked, at the same indent")]
    [InlineData("- one‸", "- one\n- ‸")]
    [InlineData("* one‸", "* one\n* ‸")]
    [InlineData("9. one‸", "9. one\n10. ‸")]
    [InlineData("1) one‸", "1) one\n2) ‸")]
    [InlineData("- [x] done‸", "- [x] done\n- [ ] ‸")]
    [InlineData("  - nested‸", "  - nested\n  - ‸")]
    [InlineData("- one‸ two", "- one\n- ‸ two")]
    [InlineData("- one‸\r\nnext", "- one\r\n- ‸\r\nnext")]
    [InlineData("first\r\n- one‸", "first\r\n- one\r\n- ‸")]
    public void ContinueList_StartsNextItem(string before, string after)
    {
        var (text, caret, _) = Parse(before);
        Assert.Equal(after, Show(text, MarkdownFormatter.ContinueList(text, caret)));
    }

    [Theory(DisplayName = "Enter on an item with no text ends the list")]
    [InlineData("- one\n- ‸", "- one\n‸")]
    [InlineData("- one\n  1. ‸", "- one\n‸")]
    [InlineData("- [ ] ‸", "‸")]
    public void ContinueList_EmptyItem_EndsList(string before, string after)
    {
        var (text, caret, _) = Parse(before);
        Assert.Equal(after, Show(text, MarkdownFormatter.ContinueList(text, caret)));
    }

    [Theory(DisplayName = "Enter outside a list item, or before its marker, is left to the editor")]
    [InlineData("plain‸")]
    [InlineData("-‸- not a list")]
    [InlineData("‸- one")]
    [InlineData("---‸")]
    public void ContinueList_NotAfterMarker_IsLeft(string before)
    {
        var (text, caret, _) = Parse(before);
        Assert.Null(MarkdownFormatter.ContinueList(text, caret));
    }

    [Theory(DisplayName = "Tab moves a list item under the text of the item above, Shift+Tab back to its parent's level")]
    [InlineData("- a\n- b‸", false, "- a\n  - b‸")]
    [InlineData("1. a\n2. b‸", false, "1. a\n   2. b‸")]
    [InlineData("1. a\n- b‸", false, "1. a\n   - b‸")]
    [InlineData("- a\n  - b‸", true, "- a\n- b‸")]
    [InlineData("- a\n  - b\n    - c‸", true, "- a\n  - b\n  - c‸")]
    public void IndentListItem_MovesTheItem(string before, bool outdent, string after)
    {
        var (text, start, length) = Parse(before);
        Assert.Equal(after, Show(text, MarkdownFormatter.IndentListItem(text, start, length, outdent)));
    }

    [Theory(DisplayName = "Tab off a list item, across lines, or Shift+Tab at the margin, is left to the editor")]
    [InlineData("plain‸", false)]
    [InlineData("«- a\n- b»", false)]
    [InlineData("- a‸", true)]
    public void IndentListItem_Otherwise_IsLeft(string before, bool outdent)
    {
        var (text, start, length) = Parse(before);
        Assert.Null(MarkdownFormatter.IndentListItem(text, start, length, outdent));
    }
}
