namespace Polhem.Markdown.UnitTests.Editing;

public class TextFinderTests
{
    private static string[] Found(string text, string query, MarkdownFindOptions options = MarkdownFindOptions.None) =>
        [.. new TextFinder(query, options).FindAll(text).Select(match => text.Substring(match.Start, match.Length))];

    [Fact(DisplayName = "A plain find ignores case and finds every place, not overlapping")]
    public void FindAll_Plain_IgnoresCase()
    {
        Assert.Equal(["Cat", "cat", "cat"], Found("Cat cat concatenate", "cat"));
        Assert.Equal([(0, 2), (2, 2)], new TextFinder("aa", MarkdownFindOptions.None).FindAll("aaaaa"));
    }

    [Fact(DisplayName = "Match case finds only the same case")]
    public void FindAll_MatchCase()
    {
        Assert.Equal(["cat"], Found("Cat cat", "cat", MarkdownFindOptions.MatchCase));
    }

    [Fact(DisplayName = "Whole word leaves out matches inside a longer word, but finds one after a part that is")]
    public void FindAll_WholeWord()
    {
        var text = "cat concat cat_x cat. (cat) catcat cat";
        var found = new TextFinder("cat", MarkdownFindOptions.WholeWord).FindAll(text);
        Assert.Equal([0, 17, 23, 35], found.Select(match => match.Start));

        // "aa" inside "aaa" is not whole, but the find goes on from the next character, not past the match.
        Assert.Equal([(4, 2)], new TextFinder("aa", MarkdownFindOptions.WholeWord).FindAll("aaa aa"));
    }

    [Fact(DisplayName = "A regular expression finds by pattern, with ^ and $ at every line, and leaves out empty matches")]
    public void FindAll_RegularExpression()
    {
        Assert.Equal(["# One", "# Two"], Found("# One\ntext\n# Two", "^# .*$", MarkdownFindOptions.RegularExpression));
        Assert.Equal(["bB"], Found("abB", "b*", MarkdownFindOptions.RegularExpression));
        Assert.Equal(["b"], Found("abB", "b+", MarkdownFindOptions.RegularExpression | MarkdownFindOptions.MatchCase));
        Assert.Equal(["cat"], Found("cat concat", "c.t", MarkdownFindOptions.RegularExpression | MarkdownFindOptions.WholeWord));
    }

    [Fact(DisplayName = "An invalid regular expression is an ArgumentException; as plain text it is found as written")]
    public void New_InvalidRegularExpression_Throws()
    {
        Assert.ThrowsAny<ArgumentException>(() => new TextFinder("(a", MarkdownFindOptions.RegularExpression));
        Assert.Equal(["(a"], Found("x(a", "(a"));
    }

    [Fact(DisplayName = "An empty query finds nothing")]
    public void FindAll_Empty_FindsNothing()
    {
        Assert.Empty(new TextFinder("", MarkdownFindOptions.None).FindAll("text"));
        Assert.Empty(new TextFinder("", MarkdownFindOptions.RegularExpression).FindAll("text"));
    }

    [Fact(DisplayName = "A replacement is literal, or for a regular expression fills in the match's groups")]
    public void Replacement_ExpandsGroupsOfRegularExpressions()
    {
        const string text = "x [one](a.md) [two](b.md)";
        var plain = new TextFinder("[one]", MarkdownFindOptions.None);
        Assert.Equal("$1", plain.Replacement(text, plain.FindAll(text)[0], "$1"));

        var regex = new TextFinder(@"\[(\w+)\]\((?<url>[^)]+)\)", MarkdownFindOptions.RegularExpression);
        var matches = regex.FindAll(text);
        Assert.Equal("<a.md|one>", regex.Replacement(text, matches[0], "<${url}|$1>"));
        Assert.Equal("<b.md|two> $", regex.Replacement(text, matches[1], "<${url}|$1> $$"));
    }

    [Fact(DisplayName = "A lookbehind still sees the text before the match when the replacement is worked out")]
    public void Replacement_Lookbehind_SeesTextBefore()
    {
        const string text = "a1 b1";
        var regex = new TextFinder(@"(?<=b)(\d)", MarkdownFindOptions.RegularExpression);
        var match = Assert.Single(regex.FindAll(text));
        Assert.Equal("[1]", regex.Replacement(text, match, "[$1]"));
    }
}
