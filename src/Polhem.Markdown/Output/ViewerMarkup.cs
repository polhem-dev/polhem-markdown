using System.Globalization;
using Polhem.Markdown.Diagrams;
using Polhem.Markdown.Highlighting;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Output;

/// <summary>
/// The class names and attribute values the Blazor viewer and <see cref="MarkdownHtml"/> both write, so that the two
/// cannot draw the same node differently and the stylesheet styles both.
/// </summary>
internal static class ViewerMarkup
{
    /// <summary>The class of a highlighted token, which the stylesheet colors through a <c>--pmd-code-*</c> variable.</summary>
    public static string TokenClass(CodeTokenKind kind) => kind switch
    {
        CodeTokenKind.Keyword => "pmd-tk-keyword",
        CodeTokenKind.String => "pmd-tk-string",
        CodeTokenKind.Comment => "pmd-tk-comment",
        CodeTokenKind.Constant => "pmd-tk-constant",
        CodeTokenKind.Tag => "pmd-tk-tag",
        _ => "pmd-tk-property",
    };

    /// <summary>The <c>text-align</c> value of a table column, or <see langword="null"/> for none.</summary>
    public static string? TextAlign(ColumnAlignment alignment) => alignment switch
    {
        ColumnAlignment.Left => "left",
        ColumnAlignment.Center => "center",
        ColumnAlignment.Right => "right",
        _ => null,
    };

    /// <summary>The class of a block centered or right-aligned by HTML.</summary>
    public static string AlignedBlockClass(BlockAlignment alignment) =>
        alignment == BlockAlignment.Right ? "pmd-align-right" : "pmd-align-center";

    /// <summary>The classes of an alert, which pick its color.</summary>
    public static string AlertClass(AlertKind kind) => "pmd-alert pmd-alert-" + kind.ToString().ToLowerInvariant();

    /// <summary>The class of a polygon in a diagram: an arrow head or the outline of a box.</summary>
    public static string PolygonClass(DiagramPolygon polygon) => polygon.IsArrowHead ? "pmd-d-arrow" : "pmd-d-box";

    /// <summary>The class of a line in a diagram.</summary>
    public static string LineClass(DiagramLine line) => line.Stroke == DiagramStroke.Rule ? "pmd-d-line pmd-d-rule" : "pmd-d-line";

    /// <summary>A diagram coordinate, written with a period whatever the current culture.</summary>
    public static string Number(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The <c>points</c> of an SVG polygon or polyline.</summary>
    public static string Points(IReadOnlyList<DiagramPoint> points) =>
        string.Join(' ', points.Select(point => Number(point.X) + "," + Number(point.Y)));

    /// <summary>The text of a link from a footnote back to a reference to it: an arrow, numbered after the first.</summary>
    public static string BackLinkText(FootnoteBackLinkNode back) => "↩" + BackLinkPosition(back);

    /// <summary>The accessible name of a link from a footnote back to a reference to it.</summary>
    public static string BackLinkLabel(FootnoteBackLinkNode back) =>
        BackLinkPosition(back) is { Length: > 0 } position ? "Back to reference " + position : "Back to reference";

    private static string BackLinkPosition(FootnoteBackLinkNode back) =>
        back.Position > 1 ? back.Position.ToString(CultureInfo.InvariantCulture) : "";
}
