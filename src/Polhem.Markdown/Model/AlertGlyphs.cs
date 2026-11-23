namespace Polhem.Markdown.Model;

/// <summary>
/// The title and icon of each <see cref="AlertKind"/>, kept here so that every viewer shows the same ones.
/// </summary>
internal static class AlertGlyphs
{
    /// <summary>The title shown above an alert. GitHub shows these English words whatever the reader's language.</summary>
    public static string Title(AlertKind kind) => kind switch
    {
        AlertKind.Tip => "Tip",
        AlertKind.Important => "Important",
        AlertKind.Warning => "Warning",
        AlertKind.Caution => "Caution",
        _ => "Note",
    };

    /// <summary>
    /// The icon of an alert as SVG path data on a 16 by 16 grid, to be filled with the even-odd rule: an outline with
    /// a mark inside it.
    /// </summary>
    public static string IconPath(AlertKind kind) => kind switch
    {
        // A light bulb.
        AlertKind.Tip => "M8 1a5 5 0 0 0-3 9v1.5h6V10a5 5 0 0 0-3-9zm0 1.5a3.5 3.5 0 0 0-2.1 6.3l.6.45V10h3V9.25l.6-.45A3.5 3.5 0 0 0 8 2.5zM5.5 12.5h5V14h-5z",

        // A speech bubble with an exclamation mark.
        AlertKind.Important => "M2 2h12a1 1 0 0 1 1 1v8a1 1 0 0 1-1 1H8l-3 3v-3H2a1 1 0 0 1-1-1V3a1 1 0 0 1 1-1zm.5 1.5v7h11v-7zM7.25 4.5h1.5V8h-1.5zm0 4.25h1.5V10h-1.5z",

        // A triangle with an exclamation mark.
        AlertKind.Warning => "M8 1.5 15 14H1zm0 3-4.4 8h8.8zM7.3 6.5h1.4v3H7.3zm0 3.75h1.4v1.25H7.3z",

        // An octagon with an exclamation mark.
        AlertKind.Caution => "M5.1 1h5.8L15 5.1v5.8L10.9 15H5.1L1 10.9V5.1zm.6 1.5L2.5 5.7v4.6l3.2 3.2h4.6l3.2-3.2V5.7l-3.2-3.2zM7.25 4.5h1.5V9h-1.5zm0 5.5h1.5v1.5h-1.5z",

        // A circle with an "i".
        _ => "M8 1a7 7 0 1 1 0 14A7 7 0 0 1 8 1zm0 1.5a5.5 5.5 0 1 0 0 11 5.5 5.5 0 0 0 0-11zM7.25 7h1.5v4.5h-1.5zM8 4.25a.9.9 0 1 1 0 1.8.9.9 0 0 1 0-1.8z",
    };
}
