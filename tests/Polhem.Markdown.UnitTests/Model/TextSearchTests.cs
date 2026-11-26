namespace Polhem.Markdown.UnitTests.Model;

/// <summary>Finding a text in a block's text (polhem-dev/polhem-markdown#85).</summary>
public class TextSearchTests
{
    [Theory(DisplayName = "A query is found wherever it occurs, without overlapping, ignoring case unless asked")]
    [InlineData("The order and the Order", "order", false, new[] { 4, 18 })]
    [InlineData("The order and the Order", "order", true, new[] { 4 })]
    [InlineData("aaaa", "aa", false, new[] { 0, 2 })]
    [InlineData("訂單與訂單明細", "訂單", false, new[] { 0, 3 })]
    [InlineData("nothing here", "missing", false, new int[0])]
    [InlineData("anything", "", false, new int[0])]
    [InlineData("ab", "abc", false, new int[0])]
    public void FindAll_FindsEveryOccurrence(string text, string query, bool matchCase, int[] starts)
    {
        var matches = TextSearch.FindAll(text, query, matchCase);

        Assert.Equal(starts, matches.Select(match => match.Start));
        Assert.All(matches, match => Assert.Equal(query.Length, match.Length));
    }
}
