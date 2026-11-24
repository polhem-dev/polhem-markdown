using System.Globalization;
using System.Text;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Html;

/// <summary>
/// Turns the HTML of a run of blocks, or of one paragraph, into model nodes, taking over only the elements in
/// <see cref="HtmlElements"/>. Tokens and the Markdown nodes between them arrive in document order.
/// </summary>
/// <remarks>
/// Elements nest as in a browser, simplified: an end tag closes the nearest open element of its name and any opened
/// inside it, an element left open closes where the run ends, and a block closes the inline elements around it. In a
/// block context, text from HTML has its white space collapsed, as a browser shows it, and inlines between blocks
/// become paragraphs. Links and images go through the same policy as Markdown's.
/// </remarks>
internal sealed class HtmlComposer
{
    private const int MaxImageSize = 4096;

    private readonly MarkdownModelBuilder _builder;
    private readonly bool _blocks;
    private readonly List<HtmlFrame> _open = [new HtmlFrame(null)];

    /// <param name="builder">Applies the link and image policy and limits nesting.</param>
    /// <param name="blocks">Whether the run is of blocks; otherwise it is the inlines of one paragraph.</param>
    public HtmlComposer(MarkdownModelBuilder builder, bool blocks)
    {
        _builder = builder;
        _blocks = blocks;
    }

    private HtmlFrame Top => _open[^1];

    public void Add(HtmlToken token)
    {
        if (Top.IsDiscarded)
        {
            if (token.Kind == HtmlTokenKind.EndTag && token.Name == Top.Name)
            {
                Close(token.End);
            }
            return;
        }
        switch (token.Kind)
        {
            case HtmlTokenKind.Text:
                var text = _blocks ? Collapse(token.Text) : token.Text;
                if (text.Length > 0)
                {
                    Top.Children.Add(new TextNode(new SourceRange(token.Start, token.Length), text));
                }
                break;
            case HtmlTokenKind.StartTag:
                Open(token);
                break;
            case HtmlTokenKind.EndTag:
                End(token);
                break;
        }
    }

    public void Add(MarkdownNode node)
    {
        if (node is BlockNode)
        {
            // NOTE: content dropped with its element ends with the HTML it was written in, so an element left open
            // does not hide the Markdown after it. A paragraph cannot hold a block, so a browser closes it.
            while (Top.IsDiscarded || IsInlineFrame(Top) || Top.Name == "p")
            {
                Close(null);
            }
        }
        else if (Top.IsDiscarded)
        {
            return;
        }
        Top.Children.Add(node);
    }

    /// <summary>The blocks of a block context, once every token and node has been added.</summary>
    public List<BlockNode> Blocks()
    {
        CloseAll();
        return ToBlocks(_open[0].Children);
    }

    /// <summary>The inlines of a paragraph, once every token and node has been added.</summary>
    public List<InlineNode> Inlines()
    {
        CloseAll();
        return [.. _open[0].Children.OfType<InlineNode>()];
    }

    private void Open(HtmlToken tag)
    {
        var name = tag.Name;
        if (HtmlElements.IsDiscarded(name))
        {
            _open.Add(new HtmlFrame(tag));
        }
        else if (name == "img")
        {
            AddImage(tag);
        }
        else if (name == "br")
        {
            Top.Children.Add(new LineBreakNode(new SourceRange(tag.Start, tag.Length)));
        }
        else if (name == "source")
        {
            AddPictureSource(tag);
        }
        else if (HtmlElements.IsInline(name))
        {
            Push(tag);
        }
        else if (_blocks && HtmlElements.IsBlock(name))
        {
            while (IsInlineFrame(Top) || Top.Name == "p")
            {
                Close(null);
            }
            if (name != "summary" || (Top.Name == "details" && Top.Summary is null))
            {
                Push(tag);
            }
        }
    }

    private void Push(HtmlToken tag)
    {
        // An element past the nesting limit is dropped, and its content kept, like an element that is not allowed.
        if (_builder.TryEnterHtml())
        {
            _open.Add(new HtmlFrame(tag));
        }
    }

    private void End(HtmlToken tag)
    {
        for (var index = _open.Count - 1; index > 0; index--)
        {
            var frame = _open[index];
            if (frame.Name == tag.Name)
            {
                while (_open.Count > index + 1)
                {
                    Close(null);
                }
                Close(tag.End);
                return;
            }

            // An inline end tag does not reach out of a block element, as text cannot be bold outside its paragraph.
            if (HtmlElements.IsBlock(frame.Name) && !HtmlElements.IsBlock(tag.Name))
            {
                return;
            }
        }
    }

    private void CloseAll()
    {
        while (_open.Count > 1)
        {
            Close(null);
        }
    }

    /// <summary>Closes the innermost element, at <paramref name="end"/>, or where its content ends when it has no end tag.</summary>
    private void Close(int? end)
    {
        var frame = _open[^1];
        _open.RemoveAt(_open.Count - 1);
        if (frame.IsDiscarded)
        {
            return;
        }
        _builder.LeaveHtml();

        var tag = frame.Tag!;
        var last = frame.Children.Count == 0 ? tag.End : frame.Children.Max(child => child.Range.End);
        var range = new SourceRange(tag.Start, Math.Max(end ?? last, tag.End) - tag.Start);
        var parent = Top;
        var inlines = frame.Children.OfType<InlineNode>().ToList();
        switch (frame.Name)
        {
            case "b" or "strong":
                AddEmphasis(parent, range, EmphasisKind.Bold, inlines);
                break;
            case "i" or "em":
                AddEmphasis(parent, range, EmphasisKind.Italic, inlines);
                break;
            case "sup":
                AddEmphasis(parent, range, EmphasisKind.Superscript, inlines);
                break;
            case "sub":
                AddEmphasis(parent, range, EmphasisKind.Subscript, inlines);
                break;
            case "kbd":
                AddEmphasis(parent, range, EmphasisKind.Keyboard, inlines);
                break;
            case "code":
                var code = PlainText(inlines);
                if (code.Length > 0)
                {
                    parent.Children.Add(new CodeSpanNode(range, code));
                }
                break;
            case "a":
                AddLink(parent, range, tag.Attribute("href"), inlines);
                break;
            case "p" or "div":
                var blocks = ToBlocks(frame.Children);
                if (blocks.Count > 0 && Alignment(tag.Attribute("align")) is { } alignment)
                {
                    parent.Children.Add(new AlignedBlockNode(range, alignment, blocks));
                }
                else
                {
                    parent.Children.AddRange(blocks);
                }
                break;
            case "details":
                var summary = frame.Summary ?? [new TextNode(new SourceRange(tag.Start, 0), "Details")];
                parent.Children.Add(new DetailsNode(range, summary, ToBlocks(frame.Children), tag.Attribute("open") is not null));
                break;
            case "summary":
                if (parent.Name == "details" && parent.Summary is null)
                {
                    parent.Summary = Trim(inlines);
                }
                else
                {
                    parent.Children.AddRange(inlines);
                }
                break;
            default:
                parent.Children.AddRange(frame.Children);
                break;
        }
    }

    private static void AddEmphasis(HtmlFrame parent, SourceRange range, EmphasisKind kind, List<InlineNode> inlines)
    {
        if (inlines.Count > 0)
        {
            parent.Children.Add(new EmphasisNode(range, kind, inlines));
        }
    }

    /// <summary>A link with content, or the content alone when it has no target or the target is refused.</summary>
    private void AddLink(HtmlFrame parent, SourceRange range, string? href, List<InlineNode> content)
    {
        if (content.Count == 0)
        {
            return;
        }
        if (href is null)
        {
            parent.Children.AddRange(content);
            return;
        }
        var linked = new List<InlineNode>();
        _builder.AddLink(range, href, content, linked);
        parent.Children.AddRange(linked);
    }

    private void AddImage(HtmlToken tag)
    {
        var range = new SourceRange(tag.Start, tag.Length);
        var image = _builder.Image(range, tag.Attribute("src"), tag.Attribute("alt") ?? "", tag.Attribute("title"));
        if (image is ImageNode node)
        {
            node = node with { Width = Pixels(tag.Attribute("width")), Height = Pixels(tag.Attribute("height")) };
            if (Top.Name == "picture")
            {
                node = node with
                {
                    DarkSource = _builder.ImageSource(FirstCandidate(Top.DarkSourceSet)),
                    LightSource = _builder.ImageSource(FirstCandidate(Top.LightSourceSet)),
                };
            }
            Top.Children.Add(node);
        }
        else if (image is TextNode { Text.Length: > 0 })
        {
            Top.Children.Add(image);
        }
    }

    /// <summary>Records a <c>source</c> of a <c>picture</c> that is chosen by the color scheme; the first of each wins.</summary>
    private void AddPictureSource(HtmlToken tag)
    {
        if (Top.Name != "picture" || tag.Attribute("srcset") is not { } sourceSet)
        {
            return;
        }
        var media = new string([.. (tag.Attribute("media") ?? "").Where(c => !char.IsWhiteSpace(c))]).ToLowerInvariant();
        if (media == "(prefers-color-scheme:dark)")
        {
            Top.DarkSourceSet ??= sourceSet;
        }
        else if (media == "(prefers-color-scheme:light)")
        {
            Top.LightSourceSet ??= sourceSet;
        }
    }

    /// <summary>The address of the first candidate of a <c>srcset</c>: what comes before its width or density.</summary>
    private static string? FirstCandidate(string? sourceSet)
    {
        var trimmed = sourceSet?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        var space = trimmed.IndexOfAny([' ', '\t', '\n', '\r', '\f']);
        return (space < 0 ? trimmed : trimmed[..space]).TrimEnd(',');
    }

    /// <summary>A size in pixels, written as a number with or without <c>px</c>; anything else, or over the limit, is no size.</summary>
    private static int? Pixels(string? value)
    {
        var text = value?.Trim();
        if (text is not null && text.EndsWith("px", StringComparison.OrdinalIgnoreCase))
        {
            text = text[..^2];
        }
        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var pixels) && pixels is > 0 and <= MaxImageSize
            ? pixels
            : null;
    }

    private static BlockAlignment? Alignment(string? align) => align?.Trim().ToLowerInvariant() switch
    {
        "center" => BlockAlignment.Center,
        "right" => BlockAlignment.Right,
        _ => null,
    };

    private static bool IsInlineFrame(HtmlFrame frame) => HtmlElements.IsInline(frame.Name) || frame.Name == "summary";

    /// <summary>Blocks from a mix of blocks and inlines: each run of inlines between blocks is a paragraph.</summary>
    private List<BlockNode> ToBlocks(List<MarkdownNode> children)
    {
        var blocks = new List<BlockNode>();
        var run = new List<InlineNode>();
        foreach (var child in children)
        {
            if (child is InlineNode inline)
            {
                run.Add(inline);
                continue;
            }
            AddParagraph(run, blocks);
            blocks.Add((BlockNode)child);
        }
        AddParagraph(run, blocks);
        return blocks;
    }

    private void AddParagraph(List<InlineNode> run, List<BlockNode> blocks)
    {
        var inlines = _blocks ? Trim(run) : [.. run];
        run.Clear();
        if (inlines.Count > 0)
        {
            var start = inlines.Min(inline => inline.Range.Start);
            blocks.Add(new ParagraphNode(new SourceRange(start, inlines.Max(inline => inline.Range.End) - start), inlines));
        }
    }

    /// <summary>
    /// Drops the spaces a browser does not show: at the start and end of a paragraph, around a line break, and the second
    /// of two in a row across nodes. Text from HTML has already been collapsed to single spaces.
    /// </summary>
    private static List<InlineNode> Trim(List<InlineNode> inlines)
    {
        var result = new List<InlineNode>(inlines.Count);
        var afterSpace = true;
        foreach (var inline in inlines)
        {
            if (inline is TextNode text)
            {
                var kept = afterSpace ? text.Text.TrimStart(' ') : text.Text;
                if (kept.Length == 0)
                {
                    continue;
                }
                result.Add(text with { Text = kept });
                afterSpace = kept[^1] == ' ';
            }
            else
            {
                if (inline is LineBreakNode)
                {
                    TrimEnd(result);
                }
                result.Add(inline);
                afterSpace = inline is LineBreakNode;
            }
        }
        TrimEnd(result);
        return result;
    }

    private static void TrimEnd(List<InlineNode> inlines)
    {
        while (inlines is [.., TextNode last] && last.Text.EndsWith(' '))
        {
            var kept = last.Text.TrimEnd(' ');
            if (kept.Length == 0)
            {
                inlines.RemoveAt(inlines.Count - 1);
            }
            else
            {
                inlines[^1] = last with { Text = kept };
            }
        }
    }

    /// <summary>Collapses each run of HTML white space to one space; a no-break space is kept.</summary>
    private static string Collapse(string text)
    {
        var result = new StringBuilder(text.Length);
        var space = false;
        foreach (var c in text)
        {
            if (c is ' ' or '\t' or '\n' or '\r' or '\f')
            {
                space = true;
                continue;
            }
            if (space)
            {
                result.Append(' ');
                space = false;
            }
            result.Append(c);
        }
        if (space)
        {
            result.Append(' ');
        }
        return result.ToString();
    }

    private static string PlainText(IEnumerable<InlineNode> inlines)
    {
        var text = new StringBuilder();
        foreach (var node in inlines.SelectMany(NodeTree.SelfAndDescendants))
        {
            text.Append(node switch
            {
                TextNode plain => plain.Text,
                CodeSpanNode code => code.Code,
                LineBreakNode => " ",
                _ => "",
            });
        }
        return text.ToString();
    }
}
