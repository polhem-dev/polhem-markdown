namespace Polhem.Markdown.Diagrams;

// NOTE: the parsed form of a flowchart is a group of small records used only between FlowchartParser and
// FlowchartLayout, so they share this file.

/// <summary>The direction a flowchart's ranks follow.</summary>
internal enum FlowDirection
{
    TopToBottom,
    BottomToTop,
    LeftToRight,
    RightToLeft,
}

/// <summary>The outline of a flowchart node.</summary>
internal enum FlowNodeShape
{
    /// <summary><c>A[text]</c>.</summary>
    Rectangle,

    /// <summary><c>A(text)</c>.</summary>
    Rounded,

    /// <summary><c>A([text])</c>.</summary>
    Stadium,

    /// <summary><c>A((text))</c>.</summary>
    Circle,

    /// <summary><c>A{text}</c>.</summary>
    Diamond,

    /// <summary><c>A{{text}}</c>.</summary>
    Hexagon,
}

/// <summary>How a flowchart link is drawn.</summary>
internal enum FlowLineStyle
{
    /// <summary><c>--&gt;</c> or <c>---</c>.</summary>
    Solid,

    /// <summary><c>-.-&gt;</c> or <c>-.-</c>.</summary>
    Dotted,

    /// <summary><c>==&gt;</c> or <c>===</c>.</summary>
    Thick,
}

/// <summary>A node, with its label split into lines.</summary>
internal sealed record FlowNode(string Id, IReadOnlyList<string> Lines, FlowNodeShape Shape);

/// <summary>A link between two nodes, given by their indexes in <see cref="Flowchart.Nodes"/>.</summary>
internal sealed record FlowEdge(int From, int To, FlowLineStyle Style, bool Arrow, IReadOnlyList<string> Label);

/// <summary>A parsed flowchart: its nodes in the order they first appear, and its links in source order.</summary>
internal sealed record Flowchart(FlowDirection Direction, IReadOnlyList<FlowNode> Nodes, IReadOnlyList<FlowEdge> Edges);
