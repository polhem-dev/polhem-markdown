using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Blazor.UnitTests;

public class SpanPatchTests
{
    /// <summary>Applies a patch to a list of spans the way the editor's script does.</summary>
    internal static void Apply(List<int[]> spans, int[] patch)
    {
        var (at, removed, delta) = (patch[0], patch[1], patch[2]);
        for (var index = at + removed; index < spans.Count; index++)
        {
            spans[index] = [spans[index][0] + delta, spans[index][1], spans[index][2]];
        }
        var inserted = new List<int[]>();
        for (var index = SyntaxSpanPatch.HeaderLength; index < patch.Length; index += SyntaxSpanPatch.Stride)
        {
            inserted.Add([patch[index], patch[index + 1], patch[index + 2]]);
        }
        spans.RemoveRange(at, removed);
        spans.InsertRange(at, inserted);
    }

    private static int[] Packed(string markdown) => SyntaxSpanPatch.Pack(MarkdownSyntaxScanner.Scan(markdown));

    private static List<int[]> Unpacked(int[] packed) =>
        Enumerable.Range(0, packed.Length / SyntaxSpanPatch.Stride)
            .Select(index => packed.AsSpan(index * SyntaxSpanPatch.Stride, SyntaxSpanPatch.Stride).ToArray())
            .ToList();

    [Theory(DisplayName = "A patch turns the spans of one text into those of the next")]
    [InlineData("", "# Title")]
    [InlineData("# Title\n\nSome **bold** text.", "# Title\n\nSome **bold** and *more* text.")]
    [InlineData("a `b` c\n\n[x](u)\n\n> q", "a `b` c\n\n> q")]
    [InlineData("**a** b **c**", "x **a** b **c**")]
    [InlineData("# One\n\n```cs\nvar x = 1;\n```", "# One\n\n```cs\nvar x = 1;\n```\n\nmore")]
    [InlineData("**a**", "")]
    public void Diff_Applied_GivesNewSpans(string before, string after)
    {
        var old = Packed(before);
        var spans = Unpacked(old);

        Apply(spans, SyntaxSpanPatch.Diff(old, before.Length, Packed(after), after.Length));

        Assert.Equal(Packed(after), spans.SelectMany(span => span).ToArray());
    }

    [Fact(DisplayName = "An edit in the middle of a long text sends only the spans around it")]
    public void Diff_EditInMiddle_SendsFewSpans()
    {
        var paragraphs = Enumerable.Range(0, 200).Select(index => $"Line {index} with **bold** and `code`.").ToList();
        var before = string.Join("\n\n", paragraphs);
        paragraphs[100] = "Line 100 with **bold**, *more* and `code`.";
        var after = string.Join("\n\n", paragraphs);

        var patch = SyntaxSpanPatch.Diff(Packed(before), before.Length, Packed(after), after.Length);

        Assert.True(patch.Length < SyntaxSpanPatch.HeaderLength + 10 * SyntaxSpanPatch.Stride, $"The patch had {patch.Length} numbers.");
    }

    [Fact(DisplayName = "A span keeps its kind and style in the packed numbers")]
    public void Pack_KeepsKindAndStyle()
    {
        var packed = Packed("**bold**");
        var spans = Unpacked(packed);

        Assert.Contains(spans, span => (span[2] & 255) == (int)MarkdownSyntaxKind.None && (span[2] >> 8) == (int)MarkdownTextStyles.Bold);
        Assert.Contains(spans, span => (span[2] & 255) == (int)MarkdownSyntaxKind.Marker);
    }

    [Theory(DisplayName = "Line breaks reach the textarea as LF and go back to the host as its text had them")]
    [InlineData("a\r\nb\r\n", "a\nb\n", true)]
    [InlineData("a\nb", "a\nb", false)]
    [InlineData("a\rb", "a\nb", false)]
    public void LineEndings_RoundTrip(string host, string textarea, bool crLf)
    {
        Assert.Equal(textarea, EditorLineEndings.ToTextarea(host));
        Assert.Equal(crLf, EditorLineEndings.UsesCrLf(host));
        Assert.Equal(crLf ? host : textarea, EditorLineEndings.ToHost(textarea, crLf));
    }

    [Theory(DisplayName = "Offsets in the textarea's text and in the host's text match up, a CRLF line break counting as two")]
    [InlineData("a\nb\nc", true, 0, 0)]
    [InlineData("a\nb\nc", true, 2, 3)]
    [InlineData("a\nb\nc", true, 4, 6)]
    [InlineData("a\nb\nc", true, 5, 7)]
    [InlineData("a\nb\nc", false, 4, 4)]
    public void Offsets_RoundTrip(string textarea, bool crLf, int offset, int hostOffset)
    {
        Assert.Equal(hostOffset, EditorLineEndings.ToHostOffset(textarea, offset, crLf));
        Assert.Equal(offset, EditorLineEndings.ToTextareaOffset(textarea, hostOffset, crLf));
    }

    [Theory(DisplayName = "A host offset inside a CRLF, or outside the text, is kept within the textarea's text")]
    [InlineData(2, 1)]
    [InlineData(99, 5)]
    [InlineData(-3, 0)]
    public void Offsets_InsideCrLfOrOutside_AreKeptWithin(int hostOffset, int expected)
    {
        Assert.Equal(expected, EditorLineEndings.ToTextareaOffset("a\nb\nc", hostOffset, crLf: hostOffset != -3));
        Assert.Equal(5, EditorLineEndings.ToHostOffset("a\nb\nc", 99, crLf: false));
    }
}
