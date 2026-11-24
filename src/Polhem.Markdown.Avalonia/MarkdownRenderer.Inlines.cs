using Avalonia;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Polhem.Markdown.Avalonia.Formulas;
using Polhem.Markdown.Avalonia.Images;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

internal sealed partial class MarkdownRenderer
{
    /// <summary>Builds selectable text from inlines, recording where each link lies in the text.</summary>
    public MarkdownTextBlock Text(IReadOnlyList<InlineNode> inlines)
    {
        var text = new MarkdownTextBlock(_viewer) { TextWrapping = TextWrapping.Wrap, Inlines = [] };
        var offset = 0;
        foreach (var inline in inlines)
        {
            text.Inlines.Add(Inline(inline, text, ref offset));
        }
        return text;
    }

    private Inline Inline(InlineNode inline, MarkdownTextBlock owner, ref int offset)
    {
        if (DrawInline(inline, owner, ref offset) is { } drawn)
        {
            _rendered.Add(inline.GetType());
            return drawn;
        }
        return new Run("");
    }

    private Inline? DrawInline(InlineNode inline, MarkdownTextBlock owner, ref int offset)
    {
        switch (inline)
        {
            case TextNode text:
                offset += text.Text.Length;
                return new Run(text.Text);

            case CodeSpanNode code:
                offset += code.Code.Length;
                var run = new Run(code.Code);
                run.Bind(TextElement.FontFamilyProperty, _viewer.GetObservable(MarkdownViewer.CodeFontFamilyProperty));
                run.Bind(TextElement.BackgroundProperty, _viewer.GetObservable(MarkdownViewer.CodeBackgroundProperty));
                return run;

            case EmphasisNode emphasis:
                var span = Emphasis(emphasis.Kind);
                foreach (var child in emphasis.Inlines)
                {
                    span.Inlines.Add(Inline(child, owner, ref offset));
                }
                return span;

            case LinkNode link:
                var start = offset;
                var linkSpan = LinkSpan(link.Inlines, owner, ref offset);
                owner.AddLink(start, offset - start, link.Target);
                return linkSpan;

            case AnchorLinkNode anchor:
                var anchorStart = offset;
                var anchorSpan = LinkSpan(anchor.Inlines, owner, ref offset);
                owner.AddAnchorLink(anchorStart, offset - anchorStart, anchor.Id);
                return anchorSpan;

            case LineBreakNode:
                offset += LineBreakLength;
                return new LineBreak();

            case ImageNode image:
                // NOTE: an embedded control takes one character of the text, which keeps link ranges after it in step.
                offset += EmbeddedControlLength;
                return new InlineUIContainer(new ImageView(_viewer, image)) { BaselineAlignment = BaselineAlignment.Center };

            case MathNode math:
                // NOTE: like an image, an embedded formula takes one character of the text.
                offset += EmbeddedControlLength;
                var formula = new FormulaView(_viewer, math.Formula, display: false, _renderedFormulas);
                owner.ReserveDepth(formula.Box.Descent);
                return new InlineUIContainer(formula);

            case FootnoteReferenceNode reference:
                var number = reference.Number.ToString(System.Globalization.CultureInfo.InvariantCulture);
                owner.AddAnchorLink(offset, number.Length, reference.NoteId);
                owner.AddAnchor(reference.Id);
                offset += number.Length;
                var superscript = new Run(number)
                {
                    BaselineAlignment = BaselineAlignment.Superscript,
                    FontSize = _viewer.GetValue(TextElement.FontSizeProperty) * SuperscriptScale,
                };
                superscript.Bind(TextElement.ForegroundProperty, _viewer.GetObservable(MarkdownViewer.LinkForegroundProperty));
                return superscript;

            case FootnoteBackLinkNode back:
                // A space keeps the arrow off the last word, as on GitHub; it is not part of the link.
                offset += 1;
                var arrow = back.Position > 1
                    ? BackArrow + back.Position.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : BackArrow;
                owner.AddAnchorLink(offset, arrow.Length, back.ReferenceId);
                offset += arrow.Length;
                var arrowRun = new Run(arrow);
                arrowRun.Bind(TextElement.ForegroundProperty, _viewer.GetObservable(MarkdownViewer.LinkForegroundProperty));
                return new Span { Inlines = { new Run(" "), arrowRun } };

            default:
                return null;
        }
    }

    private Span Emphasis(EmphasisKind kind)
    {
        switch (kind)
        {
            case EmphasisKind.Bold:
                return new Bold();
            case EmphasisKind.Strikethrough:
                return new Span { TextDecorations = TextDecorations.Strikethrough };
            case EmphasisKind.Superscript or EmphasisKind.Subscript:
                return new Span
                {
                    BaselineAlignment = kind == EmphasisKind.Superscript ? BaselineAlignment.Superscript : BaselineAlignment.Subscript,
                    FontSize = _viewer.GetValue(TextElement.FontSizeProperty) * SuperscriptScale,
                };
            case EmphasisKind.Keyboard:
                var key = new Span();
                key.Bind(TextElement.FontFamilyProperty, _viewer.GetObservable(MarkdownViewer.CodeFontFamilyProperty));
                key.Bind(TextElement.BackgroundProperty, _viewer.GetObservable(MarkdownViewer.CodeBackgroundProperty));
                return key;
            default:
                return new Italic();
        }
    }

    private Span LinkSpan(IReadOnlyList<InlineNode> inlines, MarkdownTextBlock owner, ref int offset)
    {
        var span = new Span { TextDecorations = TextDecorations.Underline };
        span.Bind(TextElement.ForegroundProperty, _viewer.GetObservable(MarkdownViewer.LinkForegroundProperty));
        foreach (var child in inlines)
        {
            span.Inlines.Add(Inline(child, owner, ref offset));
        }
        return span;
    }

    /// <summary>
    /// The number of characters a <see cref="LineBreak"/> adds to the text of a text block: one, a line feed, on every
    /// platform. <c>Render_Link_RangeMatchesText</c> checks the link offsets it produces against
    /// <see cref="InlineCollection.Text"/>.
    /// </summary>
    internal const int LineBreakLength = 1;

    /// <summary>
    /// The number of characters an <see cref="InlineUIContainer"/> adds to the text of a text block, as its layout counts
    /// them. <c>Render_LinkAfterImage_RangeMatchesText</c> checks it.
    /// </summary>
    internal const int EmbeddedControlLength = 1;

    private const string BackArrow = "\u21A9";
    private const double SuperscriptScale = 0.75;
    private const double FootnoteScale = 0.875;
}
