namespace Polhem.Markdown.Avalonia;

/// <summary>A part of the find bar of a <see cref="MarkdownViewer"/> that has a label, for <see cref="MarkdownViewer.FindBarLabels"/>.</summary>
public enum MarkdownFindBarItem
{
    /// <summary>The text box, whose placeholder and accessible name are the label.</summary>
    Field,

    /// <summary>The button that makes the find match case.</summary>
    MatchCase,

    /// <summary>The button that goes to the previous match.</summary>
    Previous,

    /// <summary>The button that goes to the next match.</summary>
    Next,

    /// <summary>The button that closes the find bar.</summary>
    Close,
}
