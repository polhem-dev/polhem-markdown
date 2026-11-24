using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

/// <summary>Raw HTML in the text: the elements README files use become nodes; any other tag is dropped and its content kept.</summary>
public class HtmlConversionTests
{
    [Theory(DisplayName = "An inline HTML element that sets text apart becomes that emphasis")]
    [InlineData("<b>x</b>", EmphasisKind.Bold)]
    [InlineData("<strong>x</strong>", EmphasisKind.Bold)]
    [InlineData("<i>x</i>", EmphasisKind.Italic)]
    [InlineData("<em>x</em>", EmphasisKind.Italic)]
    [InlineData("<sup>x</sup>", EmphasisKind.Superscript)]
    [InlineData("<sub>x</sub>", EmphasisKind.Subscript)]
    [InlineData("<kbd>x</kbd>", EmphasisKind.Keyboard)]
    [InlineData("<B>x</B>", EmphasisKind.Bold)]
    internal void Build_InlineElement_BecomesEmphasis(string html, EmphasisKind kind)
    {
        var paragraph = Single<ParagraphNode>(Parse("a " + html + " b"));

        var emphasis = Assert.Single(paragraph.Inlines.OfType<EmphasisNode>());
        Assert.Equal(kind, emphasis.Kind);
        Assert.Equal("a x b", Text(paragraph.Inlines));
    }

    [Fact(DisplayName = "Markdown inside an inline HTML element keeps its own formatting")]
    public void Build_MarkdownInsideInlineElement_IsKept()
    {
        var paragraph = Single<ParagraphNode>(Parse("<sup>*x*</sup>"));

        var sup = Assert.IsType<EmphasisNode>(Assert.Single(paragraph.Inlines));
        Assert.Equal(EmphasisKind.Italic, Assert.IsType<EmphasisNode>(Assert.Single(sup.Inlines)).Kind);
    }

    [Fact(DisplayName = "An element left open ends with its paragraph, and an end tag without a start is dropped")]
    public void Build_UnbalancedTags_CloseAtParagraphEnd()
    {
        var document = Parse("a <b>bold\n\nplain</b> text");

        var first = Assert.IsType<ParagraphNode>(document.Blocks[0]);
        Assert.Equal(EmphasisKind.Bold, Assert.IsType<EmphasisNode>(first.Inlines[1]).Kind);
        Assert.Equal("plain text", Text(Assert.IsType<ParagraphNode>(document.Blocks[1]).Inlines));
    }

    [Fact(DisplayName = "An HTML code element becomes inline code of its text")]
    public void Build_CodeElement_BecomesCodeSpan()
    {
        var paragraph = Single<ParagraphNode>(Parse("run <code>a &lt; b</code>"));

        Assert.Equal("a < b", Assert.IsType<CodeSpanNode>(paragraph.Inlines[^1]).Code);
    }

    [Fact(DisplayName = "An HTML line break becomes a line break")]
    public void Build_BreakElement_BecomesLineBreak()
    {
        var paragraph = Single<ParagraphNode>(Parse("one<br>two<br/>three"));

        Assert.Equal("one\ntwo\nthree", Text(paragraph.Inlines));
    }

    [Fact(DisplayName = "An HTML link goes through the link policy")]
    public void Build_AnchorElement_BecomesLink()
    {
        var paragraph = Single<ParagraphNode>(Parse("see <a href=\"https://example.com/\" title=\"t\">the <b>site</b></a>"));

        var link = Assert.IsType<LinkNode>(paragraph.Inlines[^1]);
        Assert.Equal(new Uri("https://example.com/"), link.Target);
        Assert.Equal("the site", Text(link.Inlines));
    }

    [Fact(DisplayName = "An HTML link to a heading of the text moves to it")]
    public void Build_AnchorElementToHeading_BecomesAnchorLink()
    {
        var document = Parse("<a href=\"#usage\">go</a>\n\n# Usage");

        Assert.Equal("usage", Assert.Single(AllNodes(document).OfType<AnchorLinkNode>()).Id);
    }

    [Fact(DisplayName = "An HTML anchor without an address keeps only its content")]
    public void Build_AnchorWithoutHref_KeepsContent()
    {
        var paragraph = Single<ParagraphNode>(Parse("<a name=\"top\">here</a>"));

        Assert.Equal("here", Text(paragraph.Inlines));
        Assert.DoesNotContain(AllNodes(Parse("<a name=\"top\">here</a>")), node => node is LinkNode);
    }

    [Fact(DisplayName = "An HTML image keeps its alternative text, title and size")]
    public void Build_ImageElement_BecomesImage()
    {
        var paragraph = Single<ParagraphNode>(Parse("<img src=\"https://example.com/a.png\" alt=\"Logo\" title=\"T\" width=\"120\" height=\"40px\">"));

        var image = Assert.IsType<ImageNode>(Assert.Single(paragraph.Inlines));
        Assert.Equal(new Uri("https://example.com/a.png"), image.Source);
        Assert.Equal("Logo", image.AlternativeText);
        Assert.Equal("T", image.Title);
        Assert.Equal(120, image.Width);
        Assert.Equal(40, image.Height);
    }

    [Theory(DisplayName = "An image size that is not a plain number of pixels within the limit is ignored")]
    [InlineData("50%")]
    [InlineData("-3")]
    [InlineData("0")]
    [InlineData("100000")]
    [InlineData("1e3")]
    public void Build_ImageSizeNotPixels_IsIgnored(string width)
    {
        var image = Assert.Single(AllNodes(Parse($"<img src=\"https://example.com/a.png\" width=\"{width}\">")).OfType<ImageNode>());

        Assert.Null(image.Width);
    }

    [Fact(DisplayName = "An HTML image with a relative source resolves against the base")]
    public void Build_ImageElementRelative_ResolvesAgainstBase()
    {
        var options = new MarkdownOptions { BaseUri = new Uri("https://example.com/repo/") };

        var image = Assert.Single(AllNodes(Parse("<img src=\"docs/logo.png\">", options)).OfType<ImageNode>());

        Assert.Equal(new Uri("https://example.com/repo/docs/logo.png"), image.Source);
    }

    [Fact(DisplayName = "A picture with sources for dark and light themes gives the image both")]
    public void Build_Picture_GivesThemeSources()
    {
        var markdown = """
            <picture>
              <source media="(prefers-color-scheme: dark)" srcset="https://example.com/dark.png 2x">
              <source media="(prefers-color-scheme: light)" srcset="https://example.com/light.png">
              <img alt="Logo" src="https://example.com/fallback.png">
            </picture>
            """;

        var image = Assert.IsType<ImageNode>(Assert.Single(Single<ParagraphNode>(Parse(markdown)).Inlines));

        Assert.Equal(new Uri("https://example.com/fallback.png"), image.Source);
        Assert.Equal(new Uri("https://example.com/dark.png"), image.DarkSource);
        Assert.Equal(new Uri("https://example.com/light.png"), image.LightSource);
        Assert.Equal(image.DarkSource, image.SourceFor(dark: true));
    }

    [Fact(DisplayName = "A picture source whose address is refused is not used")]
    public void Build_PictureRefusedSource_IsNotUsed()
    {
        var markdown = "<picture><source media=\"(prefers-color-scheme: dark)\" srcset=\"javascript:x\"><img src=\"https://example.com/a.png\"></picture>";

        var image = Assert.Single(AllNodes(Parse(markdown)).OfType<ImageNode>());

        Assert.Null(image.DarkSource);
        Assert.Equal(image.Source, image.SourceFor(dark: true));
    }

    [Fact(DisplayName = "A centered paragraph of images, as README files open, becomes an aligned block")]
    public void Build_CenteredParagraph_BecomesAlignedBlock()
    {
        var markdown = """
            <p align="center">
              <img src="https://example.com/logo.png" alt="Logo" width="200">
              <br>
              <b>A tagline</b>
            </p>

            # Title
            """;

        var document = Parse(markdown);

        var aligned = Assert.IsType<AlignedBlockNode>(document.Blocks[0]);
        Assert.Equal(BlockAlignment.Center, aligned.Alignment);
        var paragraph = Assert.IsType<ParagraphNode>(Assert.Single(aligned.Blocks));
        Assert.Equal("Logo\nA tagline", Text(paragraph.Inlines));
        Assert.IsType<HeadingNode>(document.Blocks[1]);
    }

    [Fact(DisplayName = "A centered division holds the Markdown blocks written inside it")]
    public void Build_CenteredDivision_HoldsMarkdown()
    {
        var aligned = Single<AlignedBlockNode>(Parse("<div align=\"center\">\n\n**Bold** text\n\n- item\n\n</div>"));

        Assert.Collection(aligned.Blocks, block => Assert.IsType<ParagraphNode>(block), block => Assert.IsType<ListNode>(block));
    }

    [Theory(DisplayName = "A division without a known alignment adds no block of its own")]
    [InlineData("<div>text</div>")]
    [InlineData("<div align=\"left\">text</div>")]
    [InlineData("<div align=\"justify\">text</div>")]
    public void Build_PlainDivision_IsTransparent(string markdown)
    {
        Assert.Equal("text", Text(Single<ParagraphNode>(Parse(markdown)).Inlines));
    }

    [Fact(DisplayName = "A paragraph opened in HTML ends where a Markdown block starts, as a browser closes it")]
    public void Build_HtmlParagraphBeforeMarkdownBlock_Closes()
    {
        var document = Parse("<p align=\"center\">\n\n# Title\n\n</p>");

        Assert.IsType<HeadingNode>(Assert.Single(document.Blocks));
    }

    [Fact(DisplayName = "A details element becomes a section with its summary and the Markdown inside")]
    public void Build_Details_BecomesSection()
    {
        var markdown = """
            <details>
            <summary>Click <b>me</b></summary>

            Some *markdown*.

            ```cs
            var x = 1;
            ```

            </details>

            After
            """;

        var document = Parse(markdown);

        var details = Assert.IsType<DetailsNode>(document.Blocks[0]);
        Assert.False(details.IsOpen);
        Assert.Equal("Click me", Text(details.Summary));
        Assert.Collection(details.Blocks, block => Assert.IsType<ParagraphNode>(block), block => Assert.IsType<CodeBlockNode>(block));
        Assert.Equal("After", Text(Assert.IsType<ParagraphNode>(document.Blocks[1]).Inlines));
        Assert.Equal(new SourceRange(0, markdown.IndexOf("</details>", StringComparison.Ordinal) + "</details>".Length), details.Range);
    }

    [Fact(DisplayName = "A details element written on one line, open, holds its text")]
    public void Build_DetailsOneLine_HoldsText()
    {
        var details = Single<DetailsNode>(Parse("<details open><summary>More</summary>Hidden text</details>"));

        Assert.True(details.IsOpen);
        Assert.Equal("More", Text(details.Summary));
        Assert.Equal("Hidden text", Text(Assert.IsType<ParagraphNode>(Assert.Single(details.Blocks)).Inlines));
    }

    [Fact(DisplayName = "A details element without a summary is summarized as Details, as a browser does")]
    public void Build_DetailsWithoutSummary_SaysDetails()
    {
        var details = Single<DetailsNode>(Parse("<details>\n\ntext\n\n</details>"));

        Assert.Equal("Details", Text(details.Summary));
    }

    [Fact(DisplayName = "A details element left open ends with its container")]
    public void Build_DetailsLeftOpen_EndsWithContainer()
    {
        var details = Single<DetailsNode>(Parse("<details>\n<summary>S</summary>\n\nbody"));

        Assert.Equal("body", Text(Assert.IsType<ParagraphNode>(Assert.Single(details.Blocks)).Inlines));
    }

    [Fact(DisplayName = "Details nested in details keep their nesting")]
    public void Build_NestedDetails_AreKept()
    {
        var outer = Single<DetailsNode>(Parse("<details><summary>A</summary>\n\n<details><summary>B</summary>\n\nx\n\n</details>\n\n</details>"));

        Assert.Equal("B", Text(Assert.IsType<DetailsNode>(Assert.Single(outer.Blocks)).Summary));
    }

    [Fact(DisplayName = "White space in an HTML block shows as a browser shows it")]
    public void Build_HtmlBlockWhiteSpace_IsCollapsed()
    {
        var paragraph = Single<ParagraphNode>(Parse("<div>\n   one\n\t two&nbsp; three  <br>\n   four\n</div>"));

        Assert.Equal("one two  three\nfour", Text(paragraph.Inlines));
    }

    [Fact(DisplayName = "Markdown is not read inside an HTML block, as CommonMark says")]
    public void Build_MarkdownInsideHtmlBlock_IsText()
    {
        var paragraph = Single<ParagraphNode>(Parse("<div>**not bold**</div>"));

        Assert.Equal("**not bold**", Text(paragraph.Inlines));
    }

    [Fact(DisplayName = "An HTML block in a quote is converted, with ranges in the source")]
    public void Build_HtmlInQuote_HasSourceRanges()
    {
        const string markdown = "> <sup>x</sup>\n> <div align=\"right\">y</div>";

        var quote = Single<QuoteNode>(Parse(markdown));

        var aligned = Assert.IsType<AlignedBlockNode>(quote.Blocks[^1]);
        Assert.Equal("<div align=\"right\">y</div>", markdown.Substring(aligned.Range.Start, aligned.Range.Length));
    }

    [Fact(DisplayName = "HTML nested past the limit keeps its content without more nesting")]
    public void Build_DeepHtml_IsCapped()
    {
        var markdown = string.Concat(Enumerable.Repeat("<details>", 500)) + "x";

        var depth = Depth(Single<DetailsNode>(Parse(markdown)));

        Assert.InRange(depth, 1, 20);
    }

    [Fact(DisplayName = "Inline HTML nested past the limit keeps its content without more nesting")]
    public void Build_DeepInlineHtml_IsCapped()
    {
        var markdown = string.Concat(Enumerable.Repeat("<b>", 5000)) + "x";

        var paragraph = Single<ParagraphNode>(Parse(markdown));

        Assert.Equal("x", Text(paragraph.Inlines));
        Assert.InRange(Depth(paragraph), 1, 20);
    }

    [Theory(DisplayName = "Malformed HTML shows as text and never fails")]
    [InlineData("<div title=\"never closed")]
    [InlineData("<details><summary")]
    [InlineData("a < b > c")]
    [InlineData("<div>\n<b></i></b></div></div></p>")]
    public void Build_MalformedHtml_DoesNotFail(string markdown)
    {
        _ = AllNodes(Parse(markdown)).ToList();
    }

    private static int Depth(MarkdownNode node) => 1 + (node switch
    {
        DetailsNode details => details.Blocks.Select(Depth).DefaultIfEmpty(0).Max(),
        AlignedBlockNode aligned => aligned.Blocks.Select(Depth).DefaultIfEmpty(0).Max(),
        ParagraphNode paragraph => paragraph.Inlines.Select(Depth).DefaultIfEmpty(0).Max(),
        EmphasisNode emphasis => emphasis.Inlines.Select(Depth).DefaultIfEmpty(0).Max(),
        _ => 0,
    });
}
