namespace Polhem.Markdown;

/// <summary>How a Markdown editor finds a text. The options combine.</summary>
[Flags]
public enum MarkdownFindOptions
{
    /// <summary>Case is ignored, a match may be part of a word, and the text is found as it is written.</summary>
    None = 0,

    /// <summary>Upper and lower case must match.</summary>
    MatchCase = 1,

    /// <summary>
    /// A match must not have a letter, a digit or an underscore right before or right after it, so it is not part of a
    /// longer word.
    /// </summary>
    WholeWord = 2,

    /// <summary>
    /// The text is a .NET regular expression, in which <c>^</c> and <c>$</c> match at the start and the end of every
    /// line. A replacement may refer to its groups, as <c>$1</c> or <c>${name}</c>; <c>$$</c> is a dollar sign.
    /// </summary>
    RegularExpression = 4,
}
