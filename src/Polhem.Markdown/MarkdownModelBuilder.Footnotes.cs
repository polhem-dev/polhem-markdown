using System.Globalization;
using Markdig.Extensions.Footnotes;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Polhem.Markdown.Model;

namespace Polhem.Markdown;

internal sealed partial class MarkdownModelBuilder
{
    /// <summary>
    /// The referenced notes, in number order. Markdig numbers them by first reference, drops the ones nothing refers to,
    /// and ends each note with a back link per reference.
    /// </summary>
    private FootnotesNode? Footnotes(FootnoteGroup group)
    {
        var notes = group.OfType<Footnote>()
            .Where(_noteNumbers.ContainsKey)
            .OrderBy(note => _noteNumbers[note])
            .Select(note => new FootnoteNode(Range(note), _noteNumbers[note], NoteId(note), Blocks(note)))
            .ToList();
        if (notes.Count == 0)
        {
            return null;
        }
        var start = notes.Min(note => note.Range.Start);
        return new FootnotesNode(new SourceRange(start, notes.Max(note => note.Range.End) - start), notes);
    }

    /// <summary>
    /// Numbers the notes a reader can reach, in the order they are first reached: references in the text, in order, then
    /// references inside the notes so numbered. Each back link gets its position among its note's shown references.
    /// Every note and every reference is visited once.
    /// </summary>
    /// <remarks>
    /// Markdig's own numbers count references this builder does not show: one in an image's alternative text, which
    /// shows as plain text, and one inside a note that is not shown. Either would leave a note, or a back link, that
    /// nothing in the text leads to.
    /// </remarks>
    private void NumberFootnotes(MarkdownDocument document)
    {
        var hidden = document.Descendants<LinkInline>()
            .Where(link => link.IsImage)
            .SelectMany(image => image.Descendants<FootnoteLink>())
            .Select(link => link.Index)
            .ToHashSet();
        var shown = new HashSet<int>();

        // The text comes first; each note joins the queue when it is first reached, so its own references are read after
        // everything reached before it.
        var pending = new Queue<IEnumerable<FootnoteLink>>();
        pending.Enqueue(document.Where(block => block is not FootnoteGroup).SelectMany(FootnoteLinks));
        while (pending.TryDequeue(out var links))
        {
            foreach (var link in links)
            {
                if (link.IsBackLink || link.Footnote is not { } note || hidden.Contains(link.Index))
                {
                    continue;
                }
                shown.Add(link.Index);
                if (_noteNumbers.TryAdd(note, _noteNumbers.Count + 1))
                {
                    pending.Enqueue(FootnoteLinks(note));
                }
            }
        }

        foreach (var note in _noteNumbers.Keys)
        {
            var position = 0;
            foreach (var link in note.Links.Where(link => shown.Contains(link.Index)))
            {
                _backLinkPositions[link.Index] = ++position;
            }
        }
    }

    /// <summary>The footnote links in a block and the blocks inside it, in document order.</summary>
    /// <remarks>Markdig's block descendants stop at blocks, so the inlines of each leaf block are walked here.</remarks>
    private static IEnumerable<FootnoteLink> FootnoteLinks(Block block) => block switch
    {
        LeafBlock { Inline: { } inline } => inline.Descendants<FootnoteLink>(),
        ContainerBlock container => container.SelectMany(FootnoteLinks),
        _ => [],
    };

    // NOTE: a colon never appears in a heading id (see `HeadingSlugger`), so these ids cannot match a heading.
    private string NoteId(Footnote note) => "fn:" + _noteNumbers[note].ToString(CultureInfo.InvariantCulture);

    private static string ReferenceId(FootnoteLink link) => "fnref:" + link.Index.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// A back link from a note to one of its references. Markdig adds it with no source of its own, so it is placed at the
    /// end of the paragraph that holds it.
    /// </summary>
    private FootnoteBackLinkNode BackLink(FootnoteLink back, int position)
    {
        var end = back.Parent?.ParentBlock is { } paragraph ? Range(paragraph).End : 0;
        return new FootnoteBackLinkNode(new SourceRange(end, 0), ReferenceId(back), position);
    }
}
