namespace Polhem.Markdown.Model;

/// <summary>The kinds of GitHub alert, written <c>&gt; [!NOTE]</c> and so on.</summary>
internal enum AlertKind
{
    /// <summary>Information the reader should notice even when skimming.</summary>
    Note,

    /// <summary>Optional advice that helps the reader do something better.</summary>
    Tip,

    /// <summary>Information the reader needs to succeed.</summary>
    Important,

    /// <summary>Urgent information that needs immediate attention to avoid problems.</summary>
    Warning,

    /// <summary>Advice about the risks or negative outcomes of an action.</summary>
    Caution,
}
