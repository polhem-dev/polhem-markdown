namespace Polhem.Markdown.Model;

/// <summary>Inline code.</summary>
internal sealed record CodeSpanNode(SourceRange Range, string Code) : InlineNode(Range);
