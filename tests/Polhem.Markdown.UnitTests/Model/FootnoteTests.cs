using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

public class FootnoteTests
{
    [Fact(DisplayName = "Notes are numbered by first reference, whatever their labels, and show last")]
    public void Build_Footnotes_NumberedByFirstReference()
    {
        var document = Parse("First[^z] then[^a].\n\n[^a]: Note A.\n\n[^z]: Note Z.\n\nAfter.");

        var references = AllNodes(document).OfType<FootnoteReferenceNode>().ToList();
        Assert.Equal([1, 2], references.Select(reference => reference.Number));
        var footnotes = Assert.IsType<FootnotesNode>(document.Blocks[^1]);
        Assert.Equal(["Note Z. \u21A9", "Note A. \u21A9"], footnotes.Notes.Select(NoteText));
        Assert.Equal([1, 2], footnotes.Notes.Select(note => note.Number));
        Assert.Equal(references.Select(reference => reference.NoteId), footnotes.Notes.Select(note => note.Id));
    }

    [Fact(DisplayName = "The definitions do not show where they were written")]
    public void Build_Definitions_AreNotInPlace()
    {
        var document = Parse("Text[^1].\n\n[^1]: The note.\n\nAfter.");

        Assert.Equal(["Text[1].", "After."], document.Blocks.OfType<ParagraphNode>().Select(paragraph => Text(paragraph.Inlines)));
    }

    [Fact(DisplayName = "A note referenced twice keeps its number and has a back link to each reference")]
    public void Build_RepeatedReference_HasBackLinkPerReference()
    {
        var document = Parse("One[^n] and two[^n].\n\n[^n]: The note.");

        var references = AllNodes(document).OfType<FootnoteReferenceNode>().ToList();
        Assert.All(references, reference => Assert.Equal(1, reference.Number));
        Assert.NotEqual(references[0].Id, references[1].Id);

        var note = Assert.Single(Assert.IsType<FootnotesNode>(document.Blocks[^1]).Notes);
        var backLinks = AllNodes(document).OfType<FootnoteBackLinkNode>().ToList();
        Assert.Equal(references.Select(reference => reference.Id), backLinks.Select(back => back.ReferenceId));
        Assert.Equal([1, 2], backLinks.Select(back => back.Position));
        Assert.Equal("The note. ↩ ↩2", NoteText(note));
    }

    [Fact(DisplayName = "A note nothing refers to is not shown")]
    public void Build_UnreferencedNote_IsDropped()
    {
        var document = Parse("Text[^used].\n\n[^used]: Shown.\n\n[^unused]: Not shown.");

        Assert.Equal(["Shown. \u21A9"], Assert.IsType<FootnotesNode>(document.Blocks[^1]).Notes.Select(NoteText));
    }

    [Fact(DisplayName = "A text whose notes are all unreferenced has no footnotes at all")]
    public void Build_OnlyUnreferencedNotes_NoFootnotesBlock()
    {
        var document = Parse("Text.\n\n[^lonely]: Not shown.");

        Assert.DoesNotContain(document.Blocks, block => block is FootnotesNode);
    }

    [Fact(DisplayName = "A reference to a note that is not defined shows as written")]
    public void Build_UndefinedReference_ShowsAsWritten()
    {
        var document = Parse("Text[^missing].");

        Assert.Equal("Text[^missing].", Text(Single<ParagraphNode>(document).Inlines));
        Assert.DoesNotContain(AllNodes(document), node => node is FootnoteReferenceNode or FootnotesNode);
    }

    [Fact(DisplayName = "A note can hold several blocks, and its back links end the last paragraph")]
    public void Build_NoteWithBlocks_KeepsThem()
    {
        var document = Parse("Text[^n].\n\n[^n]: First paragraph.\n\n    - an item\n\n    Last paragraph.");

        var note = Assert.Single(Assert.IsType<FootnotesNode>(document.Blocks[^1]).Notes);
        Assert.Collection(note.Blocks,
            block => Assert.Equal("First paragraph.", Text(Assert.IsType<ParagraphNode>(block).Inlines)),
            block => Assert.IsType<ListNode>(block),
            block => Assert.Equal("Last paragraph. ↩", Text(Assert.IsType<ParagraphNode>(block).Inlines)));
    }

    [Fact(DisplayName = "Footnote ids cannot be mistaken for heading ids, and a link to one shows as text")]
    public void Build_FootnoteIds_AreApartFromHeadings()
    {
        var document = Parse("# fn 1\n\nText[^1] and [jump](#fn:1).\n\n[^1]: Note.");

        var heading = Assert.IsType<HeadingNode>(document.Blocks[0]);
        var note = Assert.Single(Assert.IsType<FootnotesNode>(document.Blocks[^1]).Notes);
        Assert.NotEqual(heading.Id, note.Id);
        Assert.Contains(":", note.Id, StringComparison.Ordinal);
        Assert.DoesNotContain(AllNodes(document), node => node is AnchorLinkNode);
    }

    [Fact(DisplayName = "Footnote ranges lie within the source, the back links at the end of their paragraph")]
    public void Build_FootnoteRanges_LieWithinSource()
    {
        const string Markdown = "Text[^n].\n\n[^n]: The note.";

        var document = Parse(Markdown);

        Assert.All(AllNodes(document), node =>
        {
            Assert.InRange(node.Range.Start, 0, Markdown.Length);
            Assert.InRange(node.Range.End, node.Range.Start, Markdown.Length);
        });
        Assert.Equal("[^n]", Markdown.Substring(AllNodes(document).OfType<FootnoteReferenceNode>().Single().Range.Start, 4));
        Assert.Equal(Markdown.Length, AllNodes(document).OfType<FootnoteBackLinkNode>().Single().Range.Start);
    }

    private static string NoteText(FootnoteNode note) =>
        string.Join("\n", note.Blocks.OfType<ParagraphNode>().Select(paragraph => Text(paragraph.Inlines)));
}
