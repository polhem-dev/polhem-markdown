namespace Polhem.Markdown;

/// <summary>Finds a text in the text of a viewer's block, for finding in a viewer (polhem-dev/polhem-markdown#85).</summary>
internal static class TextSearch
{
    /// <summary>
    /// The places <paramref name="query"/> occurs in <paramref name="text"/>, from the start and not overlapping, as the
    /// start and length of each. Unless <paramref name="matchCase"/>, case is ignored, character by character, so a match
    /// is as long as the query.
    /// </summary>
    public static IReadOnlyList<(int Start, int Length)> FindAll(string text, string query, bool matchCase)
    {
        if (query.Length == 0 || text.Length < query.Length)
        {
            return [];
        }
        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var matches = new List<(int Start, int Length)>();
        var index = text.IndexOf(query, comparison);
        while (index >= 0)
        {
            matches.Add((index, query.Length));
            index = text.IndexOf(query, index + query.Length, comparison);
        }
        return matches;
    }
}
