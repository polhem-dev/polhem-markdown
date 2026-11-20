namespace Polhem.Markdown.Model;

/// <summary>A code block. <see cref="Language"/> is the first word after the opening fence, or empty.</summary>
internal sealed record CodeBlockNode(SourceRange Range, string Language, string Code) : BlockNode(Range);
