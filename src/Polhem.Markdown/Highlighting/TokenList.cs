namespace Polhem.Markdown.Highlighting;

/// <summary>Collects the tokens a scanner finds, skipping empty ones.</summary>
internal sealed class TokenList
{
    private readonly List<CodeToken> _tokens = [];

    public IReadOnlyList<CodeToken> Tokens => _tokens;

    public void Add(int start, int end, CodeTokenKind kind)
    {
        if (end > start)
        {
            _tokens.Add(new CodeToken(start, end - start, kind));
        }
    }
}
