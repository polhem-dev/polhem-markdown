namespace Polhem.Markdown.Model;

/// <summary>A hard line break. A soft line break, a single newline inside a paragraph, becomes a space instead.</summary>
internal sealed record LineBreakNode(SourceRange Range) : InlineNode(Range);
