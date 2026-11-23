namespace Polhem.Markdown.Highlighting;

/// <summary>A highlighted stretch of a code block's code: <see cref="Length"/> characters from <see cref="Start"/>.</summary>
internal readonly record struct CodeToken(int Start, int Length, CodeTokenKind Kind)
{
    /// <summary>The index just after the token.</summary>
    public int End => Start + Length;
}
