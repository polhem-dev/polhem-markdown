using Polhem.Markdown.Model;

namespace Polhem.Markdown.Editing;

/// <summary>
/// Collects the kind and style of every character of a source, then hands them out as runs. Syntax nests (a bold word in
/// a link in a heading), so the scanner paints outer constructs first and inner ones over them: a later kind replaces an
/// earlier one, while styles add up.
/// </summary>
internal sealed class SyntaxPainter(int length)
{
    private readonly MarkdownSyntaxKind[] _kinds = new MarkdownSyntaxKind[length];
    private readonly MarkdownTextStyles[] _styles = new MarkdownTextStyles[length];

    /// <summary>Gives <paramref name="range"/> the <paramref name="kind"/>, replacing what it had.</summary>
    public void Paint(SourceRange range, MarkdownSyntaxKind kind)
    {
        var (start, end) = Clamp(range);
        _kinds.AsSpan(start, end - start).Fill(kind);
    }

    /// <summary>Adds <paramref name="style"/> to the style of <paramref name="range"/>.</summary>
    public void AddStyle(SourceRange range, MarkdownTextStyles style)
    {
        var (start, end) = Clamp(range);
        for (var index = start; index < end; index++)
        {
            _styles[index] |= style;
        }
    }

    /// <summary>The runs of equal kind and style, in order, leaving out plain text.</summary>
    public List<SyntaxSpan> Runs()
    {
        var runs = new List<SyntaxSpan>();
        var start = 0;
        for (var index = 1; index <= _kinds.Length; index++)
        {
            if (index < _kinds.Length && _kinds[index] == _kinds[start] && _styles[index] == _styles[start])
            {
                continue;
            }
            if (_kinds[start] != MarkdownSyntaxKind.None || _styles[start] != MarkdownTextStyles.None)
            {
                runs.Add(new SyntaxSpan(new SourceRange(start, index - start), _kinds[start], _styles[start]));
            }
            start = index;
        }
        return runs;
    }

    private (int Start, int End) Clamp(SourceRange range)
    {
        var start = Math.Clamp(range.Start, 0, _kinds.Length);
        return (start, Math.Clamp(range.End, start, _kinds.Length));
    }
}
