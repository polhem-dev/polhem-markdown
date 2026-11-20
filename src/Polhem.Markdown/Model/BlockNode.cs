namespace Polhem.Markdown.Model;

/// <summary>A node that occupies its own vertical space: a paragraph, a list, a table.</summary>
internal abstract record BlockNode(SourceRange Range) : MarkdownNode(Range);
