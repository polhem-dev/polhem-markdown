using Avalonia.Controls;
using Avalonia.Media;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// The control a <see cref="FindBar"/> finds in, <see cref="MarkdownViewer"/> or <see cref="MarkdownEditor"/>: the
/// bar's look, and what its fields and buttons do. One bar serves both, so find looks the same in the viewer and the
/// editor (polhem-dev/polhem-markdown#97).
/// </summary>
internal interface IFindBarOwner
{
    /// <summary>The control itself, whose text color the bar's icons take.</summary>
    Control Control { get; }

    /// <summary>The host's labels for the bar, or <see langword="null"/> for the English ones.</summary>
    Func<MarkdownFindBarItem, string?>? FindBarLabels { get; }

    /// <summary>The brush of the bar's border.</summary>
    IBrush FindBarBorderBrush { get; }

    /// <summary>The background of a button under the pointer, and of an option that is on.</summary>
    IBrush FindBarHoverBrush { get; }

    /// <summary>The text color of an option that is on.</summary>
    IBrush FindBarAccentBrush { get; }

    /// <summary>Whether the theme is dark, which picks the bar's background.</summary>
    bool IsDarkTheme { get; }

    /// <summary>Finds the text of the bar's field, as the user types it or changes an option.</summary>
    void FindFromBar(string text, MarkdownFindOptions options);

    /// <summary>Goes to the next match. Returns whether there is one.</summary>
    bool FindNext();

    /// <summary>Goes to the previous match. Returns whether there is one.</summary>
    bool FindPrevious();

    /// <summary>Closes the bar and takes the highlights away.</summary>
    void CloseFind();

    /// <summary>Replaces the current match, or every match, with the text of the bar's replace field.</summary>
    void ReplaceFromBar(string replacement, bool all);
}
