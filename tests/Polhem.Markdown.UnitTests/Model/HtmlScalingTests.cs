using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

/// <summary>Raw HTML comes from the text, so reading it must take time in proportion to its length whatever it holds.</summary>
[Collection(ScalingCollection.Name)]
public class HtmlScalingTests
{
    [Theory(DisplayName = "Reading HTML takes time in proportion to its length")]
    [Trait("Category", Scaling.Category)]
    [InlineData("<div>\n", "<a b=\"", "a tag cut off inside an attribute value, repeated")]
    [InlineData("<div>\n", "<!-- ", "comments never closed")]
    [InlineData("<div>\n", "<script>", "scripts never closed")]
    [InlineData("<div>\n", "</b", "end tags never closed")]
    [InlineData("<div>\n", "<b><i>x</b></i>", "end tags that close many elements")]
    [InlineData("<div>\n", "<details><summary>s</summary>", "sections never closed")]
    [InlineData("", "<b>x", "inline elements never closed")]
    [InlineData("", "</b>x", "inline end tags without a start")]
    public void Build_Html_IsLinear(string prefix, string unit, string what)
    {
        string Html(int count) => prefix + string.Concat(Enumerable.Repeat(unit, count));

        Scaling.AssertLinear(Html, markdown => Parse(markdown), 500, 40_000, what);
    }
}
