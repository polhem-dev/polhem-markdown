using Polhem.Markdown.Editing;

namespace Polhem.Markdown.UnitTests.Editing;

/// <summary>
/// The editor scans its whole text again after every pause in typing, so scanning must take time in proportion to the
/// text's length whatever it holds. Nested constructs are painted once per level, which is where a slow path would hide.
/// </summary>
[Collection(ScalingCollection.Name)]
public class MarkdownSyntaxScannerScalingTests
{
    [Theory(DisplayName = "Scanning takes time in proportion to the text's length")]
    [Trait("Category", Scaling.Category)]
    // NOTE: three inputs are left out because Markdig itself does not read them in linear time, for the viewers as much as
    // for this scanner: many emphasis delimiters in one paragraph, closed or not, and many unmatched `[` before a link.
    // Quotes nested in quotes are refused past 127 levels, so a larger input times Markdig's exception instead. How deep
    // nesting is scanned is covered by `Scan_DeepNesting_DoesNotOverflowTheStack`.
    [InlineData("", "[a](https://b) ", "", "links")]
    [InlineData("", "> a\n", "", "a quote with many lines")]
    [InlineData("", "- a\n", "", "a list with many items")]
    [InlineData("| a | b |\n|---|---|\n", "| 1 | 2 |\n", "", "a table with many rows")]
    [InlineData("```cs\n", "var x = \"a\"; // c\n", "```", "highlighted code")]
    [InlineData("", "<b>x</b> ", "", "inline HTML")]
    public void Scan_IsLinear(string prefix, string unit, string suffix, string what)
    {
        string Text(int count) => prefix + string.Concat(Enumerable.Repeat(unit, count)) + suffix;

        Scaling.AssertLinear(Text, markdown => MarkdownSyntaxScanner.Scan(markdown), 500, 1_000_000, what);
    }
}
