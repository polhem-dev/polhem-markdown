namespace Polhem.Markdown.Model;

/// <summary>A heading; <see cref="Level"/> runs from 1 to 6.</summary>
internal sealed record HeadingNode(SourceRange Range, int Level, IReadOnlyList<InlineNode> Inlines) : BlockNode(Range);
