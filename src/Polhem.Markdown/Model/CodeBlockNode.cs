using Polhem.Markdown.Highlighting;

namespace Polhem.Markdown.Model;

/// <summary>
/// A code block. <see cref="Language"/> is the fence's info string, or empty. <see cref="Tokens"/> are the highlighted
/// stretches of <see cref="Code"/>, in order; the text between them, and all of it in a language the highlighter does
/// not know, is plain.
/// </summary>
internal sealed record CodeBlockNode(SourceRange Range, string Language, string Code, IReadOnlyList<CodeToken> Tokens) : BlockNode(Range);
