using Polhem.Markdown.Editing;
using Polhem.Markdown.Tests.Shared;

namespace Polhem.Markdown.UnitTests.Editing;

public class MarkdownSyntaxScannerTests
{
    /// <summary>Each span as <c>Kind:text</c>, or <c>Kind+Style:text</c> when it has a style, in order.</summary>
    private static List<string> Spans(string markdown) => MarkdownSyntaxScanner.Scan(markdown)
        .Select(span => span.Kind
            + (span.Style == MarkdownTextStyle.None ? "" : "+" + span.Style.ToString().Replace(", ", "+", StringComparison.Ordinal))
            + ":" + markdown.Substring(span.Range.Start, span.Range.Length))
        .ToList();

    [Fact(DisplayName = "An ATX heading: the opening and closing #s are markers, the rest of the line is the heading")]
    public void Scan_AtxHeading()
    {
        Assert.Equal(["Marker:#", "Heading: Title ", "Marker:##"], Spans("# Title ##"));
        Assert.Equal(["Marker:###", "Heading: C# 10"], Spans("### C# 10"));
    }

    [Fact(DisplayName = "A setext heading: the underline is a marker")]
    public void Scan_SetextHeading()
    {
        Assert.Equal(["Heading:Title\n", "Marker:==="], Spans("Title\n==="));
    }

    [Fact(DisplayName = "Emphasis: the delimiters are markers, and nested styles add up")]
    public void Scan_Emphasis()
    {
        Assert.Equal(
            ["Marker+Bold:**", "None+Bold:bold ", "Marker+Bold+Italic:_", "None+Bold+Italic:both", "Marker+Bold+Italic:_",
             "Marker+Bold:**", "Marker+Strikethrough:~~", "None+Strikethrough:gone", "Marker+Strikethrough:~~"],
            Spans("**bold _both_** ~~gone~~"));
    }

    [Fact(DisplayName = "Emphasis inside a heading keeps the heading's kind and adds its style")]
    public void Scan_EmphasisInHeading()
    {
        Assert.Equal(["Marker:#", "Heading: a ", "Marker+Bold:**", "Heading+Bold:b", "Marker+Bold:**"], Spans("# a **b**"));
    }

    [Fact(DisplayName = "Inline code and inline math: the delimiters are markers")]
    public void Scan_CodeAndMath()
    {
        Assert.Equal(
            ["Marker:``", "Code: a`b ", "Marker:``", "Marker:$", "Math:x^2", "Marker:$"],
            Spans("`` a`b `` and $x^2$"));
    }

    [Fact(DisplayName = "A link: brackets are markers, the text is the link's, the destination and title are its address")]
    public void Scan_Link()
    {
        Assert.Equal(
            ["Marker:[", "Link:text", "Marker:](", "Url:https://a.b", "Marker: ", "Url:\"t\"", "Marker:)"],
            Spans("[text](https://a.b \"t\")"));
    }

    [Fact(DisplayName = "An image: the ! is a marker and the alternative text is the link's")]
    public void Scan_Image()
    {
        Assert.Equal(["Marker:![", "Link:alt", "Marker:](", "Url:i.png", "Marker:)"], Spans("![alt](i.png)"));
    }

    [Fact(DisplayName = "A reference link and its definition: the label is an address, and no part is taken from elsewhere")]
    public void Scan_ReferenceLink()
    {
        Assert.Equal(
            ["Marker:[", "Link:r", "Marker:][", "Url:lab", "Marker:]", "Marker:[", "Link:lab", "Marker:]: ", "Url:https://l",
             "Marker: ", "Url:'t'"],
            Spans("[r][lab]\n\n[lab]: https://l 't'"));
    }

    [Fact(DisplayName = "Autolinks: a URL in angle brackets and a bare URL are addresses")]
    public void Scan_Autolinks()
    {
        Assert.Equal(
            ["Marker:<", "Url:https://x.y", "Marker:>", "Url:https://example.com/a", "Url:www.example.com"],
            Spans("<https://x.y> https://example.com/a www.example.com"));
    }

    [Fact(DisplayName = "Quotes: each line's > is a marker, nested ones too, and a lazy line has none")]
    public void Scan_Quote()
    {
        Assert.Equal(["Marker:>", "Marker:>", "Marker:>"], Spans("> quote\n> > nested\nlazy"));
    }

    [Fact(DisplayName = "An alert: its kind is a marker")]
    public void Scan_Alert()
    {
        Assert.Equal(["Marker:>", "Marker:[!NOTE]", "Marker:>"], Spans("> [!NOTE]\n> body"));
    }

    [Fact(DisplayName = "Lists: bullets, numbers and task boxes are markers")]
    public void Scan_Lists()
    {
        Assert.Equal(
            ["Marker:-", "Marker:*", "Marker:1.", "Marker:12)", "Marker:+", "Marker:[x]"],
            Spans("- one\n  * two\n\n1. num\n\n12) x\n\n+ [x] done"));
    }

    [Fact(DisplayName = "A table: pipes and the delimiter row are markers, cell text is text")]
    public void Scan_Table()
    {
        Assert.Equal(
            ["Marker:|", "Marker:|", "Marker:|\n|---|:-:|\n|", "Marker:| ", "Marker+Bold:**", "None+Bold:2", "Marker+Bold:**",
             "Marker:|"],
            Spans("| a | b |\n|---|:-:|\n| 1 | **2** |"));
    }

    [Fact(DisplayName = "A thematic break is a marker")]
    public void Scan_ThematicBreak()
    {
        Assert.Equal(["Marker:***"], Spans("***\n"));
    }

    [Fact(DisplayName = "Fenced code: the fences are markers and the code is highlighted as in the viewers")]
    public void Scan_FencedCode()
    {
        Assert.Equal(
            ["Marker:```cs\n", "CodeKeyword:var", "Code: s = ", "CodeString:\"x\"", "Code:; ", "CodeComment:// c", "Marker:\n```"],
            Spans("```cs\nvar s = \"x\"; // c\n```"));
    }

    [Fact(DisplayName = "Fenced code in a list or a quote: the tokens land on the source, past each line's indentation")]
    public void Scan_FencedCodeInContainers()
    {
        Assert.Equal(
            ["Marker:-", "Marker:```js\n  ", "CodeKeyword:let", "Code: a = ", "CodeConstant:1", "Code:;", "Marker:\n  ```"],
            Spans("- item\n  ```js\n  let a = 1;\n  ```"));
        Assert.Equal(
            ["Marker:>", "Marker:```cs\n> ", "CodeKeyword:int", "Code: x;", "Marker:\n> ```"],
            Spans("> ```cs\n> int x;\n> ```"));
    }

    [Fact(DisplayName = "A token that spans lines is split at each line, so the markers between them stay markers")]
    public void Scan_MultiLineToken()
    {
        Assert.Equal(
            ["Marker:>", "Marker:```cs\n> ", "CodeComment:/* a", "Marker:\n> ", "CodeComment:b */", "Marker:\n> ```"],
            Spans("> ```cs\n> /* a\n> b */\n> ```"));
    }

    [Fact(DisplayName = "Indented code is code, without its indentation")]
    public void Scan_IndentedCode()
    {
        Assert.Equal(["Code:indented"], Spans("    indented\n"));
    }

    [Fact(DisplayName = "Code in a language the highlighter does not know, and math, are code and math throughout")]
    public void Scan_PlainCodeAndMathBlock()
    {
        Assert.Equal(["Marker:```text\n", "Code:var x", "Marker:\n```"], Spans("```text\nvar x\n```"));
        Assert.Equal(["Marker:$$\n", "Math:x^2", "Marker:\n$$"], Spans("$$\nx^2\n$$"));
        Assert.Equal(["Marker:```math\n", "Math:x^2", "Marker:\n```"], Spans("```math\nx^2\n```"));
    }

    [Fact(DisplayName = "YAML front matter is highlighted as YAML")]
    public void Scan_FrontMatter()
    {
        Assert.Equal(["Marker:---\n", "CodeProperty:title", "Code:: x", "Marker:\n---"], Spans("---\ntitle: x\n---\n\ntext"));
    }

    [Fact(DisplayName = "Raw HTML, in a block or inline, is highlighted as HTML")]
    public void Scan_Html()
    {
        Assert.Equal(
            ["Html:<", "CodeTag:div", "Html: ", "CodeProperty:class", "Html:=", "CodeString:\"a\"", "Html:>"],
            Spans("<div class=\"a\">"));
        Assert.Equal(["Html:<", "CodeTag:b", "Html:>", "Html:</", "CodeTag:b", "Html:>"], Spans("a <b>x</b>"));
    }

    [Fact(DisplayName = "Footnotes: a reference is a link and a definition's label is a marker")]
    public void Scan_Footnotes()
    {
        Assert.Equal(["Link:[^1]", "Marker:[^1]:"], Spans("text[^1]\n\n[^1]: note"));
    }

    [Fact(DisplayName = "Lines ending in CRLF keep every span on its own characters")]
    public void Scan_CrLf()
    {
        Assert.Equal(
            ["Marker:#", "Heading: Head", "Marker+Bold:**", "None+Bold:b", "Marker+Bold:**", "Marker:```cs\r\n", "CodeKeyword:int",
             "Code: x;", "Marker:\r\n```"],
            Spans("# Head\r\n\r\n**b**\r\n```cs\r\nint x;\r\n```\r\n"));
    }

    [Fact(DisplayName = "Emphasis next to Chinese punctuation is found, as the viewers find it")]
    public void Scan_Cjk()
    {
        Assert.Equal(["Marker+Bold:**", "None+Bold:粗體", "Marker+Bold:**"], Spans("標題 **粗體**。"));
    }

    [Fact(DisplayName = "An empty text, and a text nested deeper than the parser allows, give no spans")]
    public void Scan_EmptyAndTooDeep()
    {
        Assert.Empty(MarkdownSyntaxScanner.Scan(""));
        Assert.Empty(MarkdownSyntaxScanner.Scan(new string('>', 5000) + " deep"));
    }

    [Fact(DisplayName = "The spans of a text with every kind of node are in order, apart, inside the text and never empty")]
    public void Scan_AllNodes_SpansAreOrderedAndApart()
    {
        var markdown = ModelNodeTypes.AllNodesMarkdown();
        var spans = MarkdownSyntaxScanner.Scan(markdown);

        Assert.NotEmpty(spans);
        var previousEnd = 0;
        foreach (var span in spans)
        {
            Assert.True(span.Range.Length > 0, $"empty span at {span.Range.Start}");
            Assert.True(span.Range.Start >= previousEnd, $"span at {span.Range.Start} overlaps the one before");
            Assert.True(span.Range.End <= markdown.Length, $"span at {span.Range.Start} runs past the text");
            previousEnd = span.Range.End;
        }
    }
}
