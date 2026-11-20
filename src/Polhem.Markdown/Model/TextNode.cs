namespace Polhem.Markdown.Model;

/// <summary>Plain text. Anything the safety policy refuses to render ends up here, shown as it was written.</summary>
internal sealed record TextNode(SourceRange Range, string Text) : InlineNode(Range);
