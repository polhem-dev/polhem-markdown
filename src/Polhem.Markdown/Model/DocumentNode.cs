namespace Polhem.Markdown.Model;

/// <summary>A parsed Markdown text: its top-level blocks.</summary>
internal sealed record DocumentNode(IReadOnlyList<BlockNode> Blocks);
