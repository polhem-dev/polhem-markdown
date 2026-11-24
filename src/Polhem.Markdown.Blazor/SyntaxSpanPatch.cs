using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Blazor;

/// <summary>
/// The spans <see cref="MarkdownSyntaxScanner"/> found, packed as numbers for the script of <see cref="MarkdownEditor"/>,
/// and the change from one scan to the next, so an edit sends the spans around it rather than all of them.
/// </summary>
/// <remarks>
/// A span is three numbers: its start, its length, and its kind with its style shifted left by eight bits. A patch is
/// the index of the first span that changed, the number of old spans it replaces, the change in the length of the text
/// (which moves every span after them), and then the new spans. The script applies it in
/// <c>polhem-markdown-editor.js</c> (<c>applyPatch</c>); both sides must read it the same way.
/// </remarks>
internal static class SyntaxSpanPatch
{
    /// <summary>The numbers per span.</summary>
    public const int Stride = 3;

    /// <summary>The numbers before the spans of a patch.</summary>
    public const int HeaderLength = 3;

    /// <summary>Packs <paramref name="spans"/> as numbers.</summary>
    public static int[] Pack(IReadOnlyList<SyntaxSpan> spans)
    {
        var packed = new int[spans.Count * Stride];
        for (var index = 0; index < spans.Count; index++)
        {
            var span = spans[index];
            packed[index * Stride] = span.Range.Start;
            packed[index * Stride + 1] = span.Range.Length;
            packed[index * Stride + 2] = (int)span.Kind | ((int)span.Style << 8);
        }
        return packed;
    }

    /// <summary>
    /// The patch that turns <paramref name="old"/>, the spans of a text <paramref name="oldLength"/> long, into
    /// <paramref name="new"/>, those of a text <paramref name="newLength"/> long. Spans equal at the start are kept, and
    /// so are those equal at the end once moved by the change in length.
    /// </summary>
    public static int[] Diff(int[] old, int oldLength, int[] @new, int newLength)
    {
        var oldCount = old.Length / Stride;
        var newCount = @new.Length / Stride;
        var delta = newLength - oldLength;

        var head = 0;
        while (head < oldCount && head < newCount && SameSpan(old, head, @new, head, 0))
        {
            head++;
        }

        var tail = 0;
        while (tail < oldCount - head && tail < newCount - head
            && SameSpan(old, oldCount - 1 - tail, @new, newCount - 1 - tail, delta))
        {
            tail++;
        }

        var inserted = newCount - head - tail;
        var patch = new int[HeaderLength + inserted * Stride];
        patch[0] = head;
        patch[1] = oldCount - head - tail;
        patch[2] = delta;
        Array.Copy(@new, head * Stride, patch, HeaderLength, inserted * Stride);
        return patch;
    }

    private static bool SameSpan(int[] old, int oldIndex, int[] @new, int newIndex, int delta) =>
        old[oldIndex * Stride] + delta == @new[newIndex * Stride]
        && old[oldIndex * Stride + 1] == @new[newIndex * Stride + 1]
        && old[oldIndex * Stride + 2] == @new[newIndex * Stride + 2];

    /// <summary>
    /// The CSS class of each <see cref="MarkdownSyntaxKind"/>, indexed by its value; an empty string for text with no
    /// color of its own. The script receives the table, so the numbers of the kinds are not written into it.
    /// </summary>
    public static string[] KindClasses()
    {
        var kinds = Enum.GetValues<MarkdownSyntaxKind>();
        var classes = new string[kinds.Max(kind => (int)kind) + 1];
        foreach (var kind in kinds)
        {
            classes[(int)kind] = KindClass(kind);
        }
        return classes;
    }

    /// <summary>The CSS class of <paramref name="kind"/>, as the editor's styles in <c>polhem-markdown.css</c> name it.</summary>
    public static string KindClass(MarkdownSyntaxKind kind) => kind switch
    {
        MarkdownSyntaxKind.None or MarkdownSyntaxKind.CodeBlock => "",

        // Raw HTML is shown as syntax, like the markers, as the Avalonia editor does.
        MarkdownSyntaxKind.Marker or MarkdownSyntaxKind.Html => "pmd-sx-marker",
        MarkdownSyntaxKind.Heading => "pmd-sx-heading",
        MarkdownSyntaxKind.Code => "pmd-sx-code",
        MarkdownSyntaxKind.Link => "pmd-sx-link",
        MarkdownSyntaxKind.Url => "pmd-sx-url",
        MarkdownSyntaxKind.Math => "pmd-sx-math",

        // The code of fenced blocks is colored like the viewer's highlighted code, with the same classes.
        MarkdownSyntaxKind.CodeKeyword => "pmd-tk-keyword",
        MarkdownSyntaxKind.CodeString => "pmd-tk-string",
        MarkdownSyntaxKind.CodeComment => "pmd-tk-comment",
        MarkdownSyntaxKind.CodeConstant => "pmd-tk-constant",
        MarkdownSyntaxKind.CodeTag => "pmd-tk-tag",
        MarkdownSyntaxKind.CodeProperty => "pmd-tk-property",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
