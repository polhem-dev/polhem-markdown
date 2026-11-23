namespace Polhem.Markdown.Model;

/// <summary>
/// A GitHub alert: a quote that starts with <c>[!NOTE]</c>, <c>[!TIP]</c>, <c>[!IMPORTANT]</c>, <c>[!WARNING]</c> or
/// <c>[!CAUTION]</c>. The marker is not part of the blocks; the viewers show a title for the kind instead.
/// </summary>
internal sealed record AlertNode(SourceRange Range, AlertKind Kind, IReadOnlyList<BlockNode> Blocks) : BlockNode(Range);
