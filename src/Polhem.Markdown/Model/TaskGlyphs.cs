namespace Polhem.Markdown.Model;

/// <summary>
/// The check mark of a task list item's box, kept here so that every viewer draws the same one rather than the check box
/// of its platform, which looks different on each.
/// </summary>
internal static class TaskGlyphs
{
    /// <summary>The check mark in a checked box, as SVG path data on a 14 by 14 grid, to be stroked rather than filled.</summary>
    public const string CheckPath = "M3.5 7.25 6 9.75 10.5 4.75";
}
