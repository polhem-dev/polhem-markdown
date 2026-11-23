using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

public class BlockConversionTests
{
    [Theory(DisplayName = "Empty or blank text produces no blocks")]
    [InlineData("")]
    [InlineData("   \n\n  \t\n")]
    public void Build_EmptyOrBlank_ReturnsNoBlocks(string markdown)
    {
        Assert.Empty(Parse(markdown).Blocks);
    }

    [Theory(DisplayName = "A heading keeps its level and text, in ATX and setext form")]
    [InlineData("# Title", 1)]
    [InlineData("### Title", 3)]
    [InlineData("Title\n---", 2)]
    public void Build_Heading_KeepsLevelAndText(string markdown, int level)
    {
        var heading = Single<HeadingNode>(Parse(markdown));

        Assert.Equal(level, heading.Level);
        Assert.Equal("Title", Text(heading.Inlines));
    }

    [Fact(DisplayName = "A single newline inside a paragraph becomes a space")]
    public void Build_SoftLineBreak_BecomesSpace()
    {
        var paragraph = Single<ParagraphNode>(Parse("first line\nsecond line"));

        Assert.Equal("first line second line", Text(paragraph.Inlines));
    }

    [Fact(DisplayName = "Blank lines separate paragraphs")]
    public void Build_TwoParagraphs_ReturnsTwoBlocks()
    {
        var document = Parse("one\n\ntwo");

        Assert.Collection(document.Blocks,
            block => Assert.Equal("one", Text(Assert.IsType<ParagraphNode>(block).Inlines)),
            block => Assert.Equal("two", Text(Assert.IsType<ParagraphNode>(block).Inlines)));
    }

    [Fact(DisplayName = "A bulleted list keeps its items in order")]
    public void Build_BulletList_KeepsItems()
    {
        var list = Single<ListNode>(Parse("- apple\n- pear"));

        Assert.False(list.IsOrdered);
        Assert.False(list.IsLoose);
        Assert.Equal(["apple", "pear"], list.Items.Select(item => Text(Assert.IsType<ParagraphNode>(Assert.Single(item.Blocks)).Inlines)));
    }

    [Fact(DisplayName = "A numbered list keeps the number it starts from")]
    public void Build_OrderedList_KeepsStart()
    {
        var list = Single<ListNode>(Parse("3. three\n4. four"));

        Assert.True(list.IsOrdered);
        Assert.Equal(3, list.Start);
        Assert.Equal(2, list.Items.Count);
    }

    [Fact(DisplayName = "A list with blank lines between its items is loose")]
    public void Build_LooseList_IsLoose()
    {
        Assert.True(Single<ListNode>(Parse("- one\n\n- two")).IsLoose);
    }

    [Fact(DisplayName = "A task list item records whether it is done, and its text no longer holds the check box")]
    public void Build_TaskList_RecordsState()
    {
        var list = Single<ListNode>(Parse("- [ ] open\n- [x] done\n- [X] also done\n- plain"));

        Assert.Equal([false, true, true, null], list.Items.Select(item => item.IsChecked));
        Assert.Equal(["open", "done", "also done", "plain"],
            list.Items.Select(item => Text(Assert.IsType<ParagraphNode>(Assert.Single(item.Blocks)).Inlines)));
    }

    [Fact(DisplayName = "A task list item's text still covers its own source after the check box is removed")]
    public void Build_TaskListText_CoversItsSource()
    {
        const string Markdown = "- [x] done";

        var item = Assert.Single(Single<ListNode>(Parse(Markdown)).Items);
        var text = Assert.IsType<TextNode>(Assert.Single(Assert.IsType<ParagraphNode>(Assert.Single(item.Blocks)).Inlines));

        Assert.Equal("done", Markdown.Substring(text.Range.Start, text.Range.Length));
    }

    [Theory(DisplayName = "Brackets that are not at the start of a list item are ordinary text")]
    [InlineData("[ ] not in a list")]
    [InlineData("- see [x] here")]
    public void Build_BracketsOutsideTaskPosition_AreText(string markdown)
    {
        Assert.Contains("[", string.Concat(AllNodes(Parse(markdown)).OfType<TextNode>().Select(text => text.Text)));
        Assert.DoesNotContain(AllNodes(Parse(markdown)).OfType<ListItemNode>(), item => item.IsChecked is not null);
    }

    [Fact(DisplayName = "A nested list sits inside its parent item")]
    public void Build_NestedList_IsInsideItem()
    {
        var list = Single<ListNode>(Parse("- parent\n  - child"));

        var item = Assert.Single(list.Items);
        var nested = Assert.IsType<ListNode>(item.Blocks[1]);
        Assert.Equal("child", Text(Assert.IsType<ParagraphNode>(Assert.Single(Assert.Single(nested.Items).Blocks)).Inlines));
    }

    [Fact(DisplayName = "A fenced code block keeps its language and its code")]
    public void Build_FencedCode_KeepsLanguageAndCode()
    {
        var code = Single<CodeBlockNode>(Parse("```csharp\nvar x = 1;\nvar y = 2;\n```"));

        Assert.Equal("csharp", code.Language);
        Assert.Equal("var x = 1;\nvar y = 2;", code.Code);
    }

    [Fact(DisplayName = "An indented code block has no language")]
    public void Build_IndentedCode_HasNoLanguage()
    {
        var code = Single<CodeBlockNode>(Parse("    var x = 1;"));

        Assert.Equal("", code.Language);
        Assert.Equal("var x = 1;", code.Code);
    }

    [Fact(DisplayName = "A quote holds the blocks inside it")]
    public void Build_Quote_HoldsBlocks()
    {
        var quote = Single<QuoteNode>(Parse("> quoted"));

        Assert.Equal("quoted", Text(Assert.IsType<ParagraphNode>(Assert.Single(quote.Blocks)).Inlines));
    }

    [Theory(DisplayName = "A quote that starts with a GitHub alert marker becomes an alert of that kind, without the marker")]
    [InlineData("NOTE", nameof(AlertKind.Note))]
    [InlineData("TIP", nameof(AlertKind.Tip))]
    [InlineData("IMPORTANT", nameof(AlertKind.Important))]
    [InlineData("WARNING", nameof(AlertKind.Warning))]
    [InlineData("CAUTION", nameof(AlertKind.Caution))]
    public void Build_Alert_KeepsKindAndBlocks(string marker, string kind)
    {
        var markdown = $"> [!{marker}]\n> Back up **first**.\n>\n> - one";

        var alert = Single<AlertNode>(Parse(markdown));

        Assert.Equal(Enum.Parse<AlertKind>(kind), alert.Kind);
        Assert.Equal(markdown, markdown.Substring(alert.Range.Start, alert.Range.Length));
        Assert.Equal("Back up first.", Text(Assert.IsType<ParagraphNode>(alert.Blocks[0]).Inlines));
        Assert.IsType<ListNode>(alert.Blocks[1]);
    }

    [Fact(DisplayName = "An alert marker GitHub does not know leaves an ordinary quote that shows the marker")]
    public void Build_UnknownAlert_IsQuoteWithMarker()
    {
        const string Markdown = "> [!FOO]\n> body";

        var quote = Single<QuoteNode>(Parse(Markdown));

        var paragraph = Assert.IsType<ParagraphNode>(Assert.Single(quote.Blocks));
        Assert.Equal("[!FOO] body", Text(paragraph.Inlines));
        Assert.Equal("[!FOO]", Markdown.Substring(paragraph.Inlines[0].Range.Start, paragraph.Inlines[0].Range.Length));
    }

    [Fact(DisplayName = "An unknown alert marker with nothing after it still shows the marker")]
    public void Build_UnknownAlertAlone_ShowsMarker()
    {
        var quote = Single<QuoteNode>(Parse("> [!FOO]"));

        Assert.Equal("[!FOO]", Text(Assert.IsType<ParagraphNode>(Assert.Single(quote.Blocks)).Inlines));
    }

    [Fact(DisplayName = "YAML front matter at the start of the text is hidden")]
    public void Build_FrontMatter_IsHidden()
    {
        const string Markdown = "---\ntitle: Install\ntags: [a, b]\n---\n# Install\n\nText.";

        var document = Parse(Markdown);

        Assert.Equal(2, document.Blocks.Count);
        Assert.Equal("Install", Text(Assert.IsType<HeadingNode>(document.Blocks[0]).Inlines));
        Assert.DoesNotContain(AllNodes(document).OfType<TextNode>(), text => text.Text.Contains("title", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "Rules later in the text are not taken for front matter")]
    public void Build_RulesAfterStart_AreNotFrontMatter()
    {
        var document = Parse("Intro\n\n---\n\nkey: value\n\n---");

        Assert.Equal(["ParagraphNode", "ThematicBreakNode", "ParagraphNode", "ThematicBreakNode"], document.Blocks.Select(block => block.GetType().Name));
    }

    [Fact(DisplayName = "A thematic break becomes a rule")]
    public void Build_ThematicBreak_BecomesRule()
    {
        Assert.Collection(Parse("above\n\n***\n\nbelow").Blocks,
            block => Assert.IsType<ParagraphNode>(block),
            block => Assert.IsType<ThematicBreakNode>(block),
            block => Assert.IsType<ParagraphNode>(block));
    }

    [Fact(DisplayName = "A table separates its header row, keeps its rows and reads the alignment of each column")]
    public void Build_Table_KeepsHeaderRowsAndAlignment()
    {
        var table = Single<TableNode>(Parse("| Name | Qty | Note | Code |\n|:-----|----:|:----:|------|\n| pen | 2 | blue | A1 |\n| ink | 10 | | B2 |"));

        Assert.Equal([ColumnAlignment.Left, ColumnAlignment.Right, ColumnAlignment.Center, ColumnAlignment.None], table.Alignments);
        Assert.Equal(["Name", "Qty", "Note", "Code"], Assert.Single(table.HeaderRows).Cells.Select(cell => Text(cell.Inlines)));
        Assert.Equal(2, table.Rows.Count);
        Assert.Equal(["pen", "2", "blue", "A1"], table.Rows[0].Cells.Select(cell => Text(cell.Inlines)));
    }

    [Fact(DisplayName = "A link reference definition is not a block, and the link that uses it resolves")]
    public void Build_ReferenceLink_Resolves()
    {
        var paragraph = Single<ParagraphNode>(Parse("See [the site][site].\n\n[site]: https://example.com/"));

        var link = Assert.Single(paragraph.Inlines.OfType<LinkNode>());
        Assert.Equal(new Uri("https://example.com/"), link.Target);
    }

    [Fact(DisplayName = "Text nested deeper than the parser allows shows as written instead of failing")]
    public void Build_TooDeeplyNested_ShowsWholeTextAsParagraph()
    {
        var markdown = new string('>', 5000) + " deep";

        var paragraph = Single<ParagraphNode>(Parse(markdown));

        Assert.Equal(markdown, Text(paragraph.Inlines));
    }

    [Fact(DisplayName = "Nesting within the parser's limit is kept")]
    public void Build_NestedQuotes_AreKept()
    {
        var quote = Single<QuoteNode>(Parse(">> inner"));

        Assert.IsType<QuoteNode>(Assert.Single(quote.Blocks));
    }
}
