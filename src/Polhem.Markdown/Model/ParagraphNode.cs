namespace Polhem.Markdown.Model;

internal sealed record ParagraphNode(SourceRange Range, IReadOnlyList<InlineNode> Inlines) : BlockNode(Range);
