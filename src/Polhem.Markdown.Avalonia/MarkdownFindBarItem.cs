namespace Polhem.Markdown.Avalonia;

/// <summary>
/// A part of the find bar that has a label, for <see cref="MarkdownViewer.FindBarLabels"/> and
/// <see cref="MarkdownEditor.FindBarLabels"/>. The viewer's bar finds only; the editor's bar also has the parts for
/// whole words, regular expressions and replacing.
/// </summary>
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

    /// <summary>The button that makes the find match whole words only, in the editor.</summary>
    WholeWord,

    /// <summary>The button that makes the find text a regular expression, in the editor.</summary>
    RegularExpression,

    /// <summary>The text box of the replacement, in the editor, whose placeholder and accessible name are the label.</summary>
    ReplaceField,

    /// <summary>The button that replaces the current match, in the editor.</summary>
    Replace,

    /// <summary>The button that replaces every match, in the editor.</summary>
    ReplaceAll,
}
