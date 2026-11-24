using Polhem.Markdown.Model;

namespace Polhem.Markdown.Editing;

/// <summary>A stretch of Markdown source with one kind and one style.</summary>
internal readonly record struct SyntaxSpan(SourceRange Range, MarkdownSyntaxKind Kind, MarkdownTextStyle Style);
