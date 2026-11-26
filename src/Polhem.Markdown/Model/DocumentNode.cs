namespace Polhem.Markdown.Model;

/// <summary>A parsed Markdown text: its top-level blocks, and its headings in document order.</summary>
internal sealed record DocumentNode(IReadOnlyList<BlockNode> Blocks)
{
    /// <summary>The headings that have an id, in document order, including those inside quotes, lists and sections.</summary>
    public IReadOnlyList<MarkdownHeading> Headings { get; init; } = [];
}
