namespace Polhem.Markdown.Model;

/// <summary>
/// The pin on the header of a reader's table of contents, kept here so that every reader draws the same one. The two
/// states share one outline and differ in a way that reads at a glance: unpinned, the pin is an outline leaning over,
/// ready to be pressed in; pinned, it stands upright and solid.
/// </summary>
internal static class OutlineGlyphs
{
    /// <summary>
    /// A push pin on a 24 by 24 grid, as SVG path data to be stroked with <see cref="PinStrokeWidth"/>, round caps and
    /// round joins, and filled as well when pinned: a closed head and an open needle, which a fill leaves untouched.
    /// </summary>
    public const string PinPath =
        "M9 3h6a1 1 0 0 1 0 2h-.5v4.6l2.8 2.6c.4.4.7.9.7 1.5V15H6v-1.3c0-.6.3-1.1.7-1.5l2.8-2.6V5H9a1 1 0 0 1 0-2zM12 15v6";

    /// <summary>The width of the pin's stroke, in units of its 24 by 24 grid.</summary>
    public const double PinStrokeWidth = 1.5;

    /// <summary>The clockwise turn, in degrees about the centre of the grid, of the pin while the list is not pinned.</summary>
    public const double UnpinnedAngle = 45;
}
