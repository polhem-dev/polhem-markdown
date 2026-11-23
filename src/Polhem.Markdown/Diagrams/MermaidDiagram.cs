namespace Polhem.Markdown.Diagrams;

/// <summary>
/// Turns the text of a <c>mermaid</c> code block into a drawing, for the diagram types and the syntax this package
/// supports: flowcharts (<see cref="FlowchartParser"/>) and sequence diagrams (<see cref="SequenceParser"/>).
/// </summary>
internal static class MermaidDiagram
{
    /// <summary>The longest diagram source that is drawn; a longer one shows as code.</summary>
    public const int MaxCodeLength = 50_000;

    /// <summary>
    /// The longest line that is parsed; a diagram with a longer one shows as code. The link patterns try label lengths
    /// up to the end of the line, so this bounds the work a single line can cause.
    /// </summary>
    public const int MaxLineLength = 1_000;

    /// <summary>
    /// Parses and lays out <paramref name="code"/>. Returns <see langword="false"/> for another diagram type, a
    /// statement outside the supported subset, or a diagram above the size limits, so the block shows as code.
    /// </summary>
    public static bool TryDraw(string code, out DiagramDrawing drawing)
    {
        drawing = null!;
        if (code.Length > MaxCodeLength)
        {
            return false;
        }

        // `%%` starts a comment line. Comments are the only lines skipped; any other unsupported line fails the parse.
        var lines = code.Split('\n')
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !line.TrimStart().StartsWith("%%", StringComparison.Ordinal))
            .SkipWhile(string.IsNullOrWhiteSpace)
            .ToList();
        if (lines.Count == 0 || lines.Any(line => line.Length > MaxLineLength))
        {
            return false;
        }

        var header = lines[0].Trim();
        var keywordEnd = header.IndexOfAny([' ', '\t', ';']);
        var keyword = keywordEnd < 0 ? header : header[..keywordEnd];
        var rest = keywordEnd < 0 ? "" : header[keywordEnd..];
        var body = lines.Skip(1);

        switch (keyword)
        {
            case "flowchart" or "graph":
                return FlowchartParser.TryParse(rest, body, out var flowchart) && FlowchartLayout.TryLayout(flowchart, out drawing);
            case "sequenceDiagram":
                if (!SequenceParser.TryParse(body.Prepend(rest.TrimStart(';')), out var sequence))
                {
                    return false;
                }
                drawing = SequenceLayout.Layout(sequence);
                return true;
            default:
                return false;
        }
    }
}
