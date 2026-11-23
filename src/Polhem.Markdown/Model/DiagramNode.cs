using Polhem.Markdown.Diagrams;

namespace Polhem.Markdown.Model;

/// <summary>
/// A <c>mermaid</c> code block that was parsed and laid out. A block that cannot be drawn stays a
/// <see cref="CodeBlockNode"/>.
/// </summary>
internal sealed record DiagramNode(SourceRange Range, DiagramDrawing Drawing) : BlockNode(Range);
