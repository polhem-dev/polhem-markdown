namespace Polhem.Markdown.Model;

/// <summary>A horizontal rule.</summary>
internal sealed record ThematicBreakNode(SourceRange Range) : BlockNode(Range);
