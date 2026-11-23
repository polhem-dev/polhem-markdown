using System.Text.RegularExpressions;

namespace Polhem.Markdown.Diagrams;

/// <summary>
/// Parses the supported subset of a Mermaid flowchart: nodes with the shapes of <see cref="FlowNodeShape"/>, chains of
/// solid, dotted and thick links with or without an arrow and a label, and statements separated by line ends or
/// semicolons.
/// </summary>
/// <remarks>
/// Anything else, such as a subgraph, a style or class statement, another node shape or link end, or <c>&amp;</c>,
/// makes the whole parse fail, so the diagram shows as its code instead of being drawn with something missing.
/// </remarks>
internal static partial class FlowchartParser
{
    /// <summary>The most nodes a diagram may have before it shows as code, which bounds the work of the layout.</summary>
    public const int MaxNodes = 200;

    /// <summary>The most links a diagram may have before it shows as code.</summary>
    public const int MaxEdges = 400;

    private static readonly string[] s_unsupportedStatements =
        ["subgraph", "end", "direction", "style", "classDef", "class", "click", "linkStyle", "accTitle", "accDescr"];

    // Longest openers first, so `([` is not read as `(` followed by text starting with `[`.
    private static readonly (string Open, string Close, FlowNodeShape Shape)[] s_shapes =
    [
        ("([", "])", FlowNodeShape.Stadium),
        ("((", "))", FlowNodeShape.Circle),
        ("{{", "}}", FlowNodeShape.Hexagon),
        ("[", "]", FlowNodeShape.Rectangle),
        ("(", ")", FlowNodeShape.Rounded),
        ("{", "}", FlowNodeShape.Diamond),
    ];

    private static readonly string[] s_unsupportedShapes = ["[(", "[[", "[/", "[\\", "(((", ">", "@{", ":::"];

    /// <summary>
    /// Parses a flowchart. <paramref name="header"/> is the first line after the <c>flowchart</c> or <c>graph</c>
    /// keyword: the direction, and possibly statements after a semicolon.
    /// </summary>
    public static bool TryParse(string header, IEnumerable<string> lines, out Flowchart flowchart)
    {
        flowchart = null!;
        var state = new ParseState();

        var separator = header.IndexOf(';', StringComparison.Ordinal);
        var directionText = (separator < 0 ? header : header[..separator]).Trim();
        if (!TryDirection(directionText, out var direction))
        {
            return false;
        }

        var statements = separator < 0 ? lines : lines.Prepend(header[(separator + 1)..]);
        foreach (var line in statements)
        {
            if (!TryLine(line, state))
            {
                return false;
            }
        }

        var nodes = state.Nodes.Select(node => new FlowNode(node.Id, TextMetrics.Lines(node.Label), node.Shape)).ToList();
        if (nodes.Count == 0)
        {
            return false;
        }
        flowchart = new Flowchart(direction, nodes, state.Edges);
        return true;
    }

    private static bool TryDirection(string text, out FlowDirection direction)
    {
        switch (text)
        {
            case "" or "TB" or "TD":
                direction = FlowDirection.TopToBottom;
                return true;
            case "BT":
                direction = FlowDirection.BottomToTop;
                return true;
            case "LR":
                direction = FlowDirection.LeftToRight;
                return true;
            case "RL":
                direction = FlowDirection.RightToLeft;
                return true;
            default:
                direction = default;
                return false;
        }
    }

    private static bool TryLine(string line, ParseState state)
    {
        var cursor = new Cursor(line);
        while (true)
        {
            cursor.SkipWhitespace();
            if (cursor.AtEnd)
            {
                return true;
            }
            if (cursor.TrySkip(";"))
            {
                continue;
            }
            if (!TryStatement(cursor, state))
            {
                return false;
            }
        }
    }

    /// <summary>A node, followed by any number of links to further nodes.</summary>
    private static bool TryStatement(Cursor cursor, ParseState state)
    {
        if (s_unsupportedStatements.Any(keyword => cursor.StartsWithWord(keyword)))
        {
            return false;
        }
        if (!TryNode(cursor, state, out var from))
        {
            return false;
        }

        while (true)
        {
            cursor.SkipWhitespace();
            if (cursor.AtEnd || cursor.Peek(";"))
            {
                return true;
            }
            if (!TryLink(cursor, out var style, out var arrow, out var label))
            {
                return false;
            }
            cursor.SkipWhitespace();
            if (!TryNode(cursor, state, out var to))
            {
                return false;
            }
            if (state.Edges.Count == MaxEdges)
            {
                return false;
            }
            state.Edges.Add(new FlowEdge(from, to, style, arrow, TextMetrics.Lines(label)));
            from = to;
        }
    }

    private static bool TryNode(Cursor cursor, ParseState state, out int index)
    {
        index = -1;
        var id = cursor.ReadIdentifier();
        if (id.Length == 0)
        {
            return false;
        }
        if (s_unsupportedShapes.Any(cursor.Peek))
        {
            return false;
        }

        string? label = null;
        var shape = FlowNodeShape.Rectangle;
        foreach (var (open, close, candidate) in s_shapes)
        {
            if (cursor.TrySkip(open))
            {
                if (!TryLabel(cursor, close, out label))
                {
                    return false;
                }
                shape = candidate;
                break;
            }
        }

        if (!state.Indexes.TryGetValue(id, out index))
        {
            if (state.Nodes.Count == MaxNodes)
            {
                return false;
            }
            index = state.Nodes.Count;
            state.Indexes.Add(id, index);
            state.Nodes.Add(new NodeDraft(id, label ?? id, shape));
        }
        else if (label is not null)
        {
            // A later definition of the same node replaces its label and shape, as in Mermaid.
            state.Nodes[index] = new NodeDraft(id, label, shape);
        }
        return true;
    }

    /// <summary>Reads a node label up to <paramref name="close"/>, unquoting it when it is written in quotes.</summary>
    private static bool TryLabel(Cursor cursor, string close, out string label)
    {
        cursor.SkipWhitespace();
        if (cursor.TrySkip("\""))
        {
            var end = cursor.IndexOf("\"");
            if (end < 0)
            {
                label = "";
                return false;
            }
            label = cursor.Take(end);
            cursor.TrySkip("\"");
            cursor.SkipWhitespace();
            return cursor.TrySkip(close);
        }

        var closing = cursor.IndexOf(close);
        if (closing < 0)
        {
            label = "";
            return false;
        }
        label = cursor.Take(closing).Trim();
        cursor.TrySkip(close);
        return true;
    }

    private static bool TryLink(Cursor cursor, out FlowLineStyle style, out bool arrow, out string label)
    {
        style = FlowLineStyle.Solid;
        arrow = false;
        label = "";

        // Circle and cross ends (`--o`, `--x`) and bidirectional links (`<-->`) are not supported.
        if (cursor.Match(UnsupportedLinkPattern()).Success)
        {
            return false;
        }

        var match = cursor.Match(PlainLinkPattern());
        if (!match.Success)
        {
            match = cursor.Match(LabeledLinkPattern());
        }
        if (!match.Success)
        {
            return false;
        }

        var op = match.Groups["op"].Value;
        style = op.Contains('.', StringComparison.Ordinal) ? FlowLineStyle.Dotted
            : op.Contains('=', StringComparison.Ordinal) ? FlowLineStyle.Thick
            : FlowLineStyle.Solid;
        arrow = op.EndsWith('>');
        label = Unquote(match.Groups["label"].Value.Trim());
        cursor.Advance(match.Length);
        return true;
    }

    private static string Unquote(string text) =>
        text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;

    // `-->`, `---`, `-.->`, `-.-`, `==>`, `===`, each with any extra length, optionally followed by `|label|`.
    [GeneratedRegex(@"\G(?<op>-{2,}>|-{3,}|-\.+->|-\.+-|={2,}>|={3,})(?:\s*\|(?<label>[^|]*)\|)?")]
    private static partial Regex PlainLinkPattern();

    // `-- label -->`, `-. label .->` and `== label ==>`, and their forms without an arrow.
    [GeneratedRegex(@"\G(?:--(?![->])\s*(?<label>[^|;]+?)\s*(?<op>-{2,}>|-{3,})|-\.(?![-.])\s*(?<label>[^|;]+?)\s*(?<op>\.-+>|\.-+)|==(?![=>])\s*(?<label>[^|;]+?)\s*(?<op>={2,}>|={3,}))")]
    private static partial Regex LabeledLinkPattern();

    [GeneratedRegex(@"\G(?:<|[-.=]+[ox](?![\p{L}\p{N}_]))")]
    private static partial Regex UnsupportedLinkPattern();

    private sealed record NodeDraft(string Id, string Label, FlowNodeShape Shape);

    private sealed class ParseState
    {
        public List<NodeDraft> Nodes { get; } = [];

        public Dictionary<string, int> Indexes { get; } = new(StringComparer.Ordinal);

        public List<FlowEdge> Edges { get; } = [];
    }

    private sealed class Cursor(string text)
    {
        private int _position;

        public bool AtEnd => _position >= text.Length;

        public void SkipWhitespace()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position]))
            {
                _position++;
            }
        }

        public bool Peek(string value) => string.CompareOrdinal(text, _position, value, 0, value.Length) == 0;

        public bool TrySkip(string value)
        {
            if (!Peek(value))
            {
                return false;
            }
            _position += value.Length;
            return true;
        }

        public bool StartsWithWord(string word) =>
            Peek(word) && (_position + word.Length == text.Length || !IsIdentifierChar(text[_position + word.Length]));

        public string ReadIdentifier()
        {
            var start = _position;
            while (_position < text.Length && IsIdentifierChar(text[_position]))
            {
                _position++;
            }
            return text[start.._position];
        }

        /// <summary>The offset of <paramref name="value"/> from the cursor, or -1.</summary>
        public int IndexOf(string value)
        {
            var found = text.IndexOf(value, _position, StringComparison.Ordinal);
            return found < 0 ? -1 : found - _position;
        }

        public string Take(int length)
        {
            var taken = text.Substring(_position, length);
            _position += length;
            return taken;
        }

        public Match Match(Regex pattern) => pattern.Match(text, _position);

        public void Advance(int length) => _position += length;

        private static bool IsIdentifierChar(char c) => char.IsLetterOrDigit(c) || c == '_';
    }
}
