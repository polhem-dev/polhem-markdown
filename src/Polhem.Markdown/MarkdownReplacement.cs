namespace Polhem.Markdown;

/// <summary>
/// One change to a range of a Markdown text: <see cref="Length"/> characters from <see cref="Start"/> replaced by
/// <see cref="Text"/>. A length of 0 inserts, and an empty text deletes.
/// </summary>
/// <param name="Start">Where the range starts in the text, in UTF-16 code units.</param>
/// <param name="Length">How many UTF-16 code units the range covers.</param>
/// <param name="Text">The text that takes the range's place.</param>
public sealed record MarkdownReplacement(int Start, int Length, string Text);
