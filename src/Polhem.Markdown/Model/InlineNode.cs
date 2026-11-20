namespace Polhem.Markdown.Model;

/// <summary>A node that flows within a line of text.</summary>
internal abstract record InlineNode(SourceRange Range) : MarkdownNode(Range);
