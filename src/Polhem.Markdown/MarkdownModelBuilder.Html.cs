using System.Text;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Polhem.Markdown.Html;
using Polhem.Markdown.Model;

namespace Polhem.Markdown;

internal sealed partial class MarkdownModelBuilder
{
    // NOTE: Markdig's depth limit does not count HTML elements, and the viewers draw nested nodes recursively, so the
    // elements open at once through HTML are capped for the whole text.
    private const int MaxHtmlNesting = 16;

    private int _htmlNesting;

    /// <summary>Opens one more level of HTML nesting, unless the limit is reached.</summary>
    internal bool TryEnterHtml()
    {
        if (_htmlNesting >= MaxHtmlNesting)
        {
            return false;
        }
        _htmlNesting++;
        return true;
    }

    internal void LeaveHtml() => _htmlNesting--;

    private List<BlockNode> BlocksWithHtml(ContainerBlock container)
    {
        var composer = new HtmlComposer(this, blocks: true);
        foreach (var block in container)
        {
            if (block is HtmlBlock html)
            {
                foreach (var token in Tokens(html))
                {
                    composer.Add(token);
                }
            }
            else if (Block(block) is { } node)
            {
                composer.Add(node);
            }
        }
        return composer.Blocks();
    }

    private List<InlineNode> InlinesWithHtml(IEnumerable<Inline> container)
    {
        var composer = new HtmlComposer(this, blocks: false);
        var converted = new List<InlineNode>();
        foreach (var inline in container)
        {
            if (inline is HtmlInline html)
            {
                var start = Range(html).Start;
                foreach (var token in HtmlTokenizer.Tokenize(html.Tag))
                {
                    var tokenStart = Math.Min(start + token.Start, _markdown.Length);
                    composer.Add(token with { Start = tokenStart, Length = Math.Min(token.Length, _markdown.Length - tokenStart) });
                }
                continue;
            }
            converted.Clear();
            AddInline(inline, converted);
            foreach (var node in converted)
            {
                composer.Add(node);
            }
        }
        return composer.Inlines();
    }

    /// <summary>
    /// The tokens of an HTML block, located in the source. Markdig gives the block's lines without the markers of the
    /// blocks around it (a quote's <c>&gt;</c>, a list item's indent), so each character is mapped back to its own place.
    /// </summary>
    private List<HtmlToken> Tokens(HtmlBlock html)
    {
        var text = new StringBuilder();
        var sourceIndex = new List<int>();
        var lineEnd = Range(html).Start;
        for (var index = 0; index < html.Lines.Count; index++)
        {
            var line = html.Lines.Lines[index];
            if (index > 0)
            {
                text.Append('\n');
                sourceIndex.Add(lineEnd);
            }

            // A line whose tab Markdig expanded is a copy, not a slice of the source; its characters map to its start.
            var slice = line.Slice;
            var inSource = ReferenceEquals(slice.Text, _markdown);
            for (var position = slice.Start; position <= slice.End; position++)
            {
                text.Append(slice.Text[position]);
                sourceIndex.Add(inSource ? position : line.Position);
            }
            lineEnd = inSource ? slice.End + 1 : line.Position;
        }
        sourceIndex.Add(lineEnd);

        var tokens = HtmlTokenizer.Tokenize(text.ToString());
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            var start = Math.Min(sourceIndex[token.Start], _markdown.Length);
            var end = Math.Clamp(sourceIndex[token.End], start, _markdown.Length);
            tokens[index] = token with { Start = start, Length = end - start };
        }
        return tokens;
    }
}
