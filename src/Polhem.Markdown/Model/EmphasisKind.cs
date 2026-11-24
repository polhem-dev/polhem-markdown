namespace Polhem.Markdown.Model;

/// <summary>How an <see cref="EmphasisNode"/> sets its text apart.</summary>
internal enum EmphasisKind
{
    /// <summary>Italic text: <c>*x*</c> or <c>_x_</c>.</summary>
    Italic,

    /// <summary>Bold text: <c>**x**</c> or <c>__x__</c>.</summary>
    Bold,

    /// <summary>Struck-through text: <c>~~x~~</c>.</summary>
    Strikethrough,

    /// <summary>Raised, smaller text: <c>&lt;sup&gt;x&lt;/sup&gt;</c>.</summary>
    Superscript,

    /// <summary>Lowered, smaller text: <c>&lt;sub&gt;x&lt;/sub&gt;</c>.</summary>
    Subscript,

    /// <summary>A key or keys to press: <c>&lt;kbd&gt;Ctrl&lt;/kbd&gt;</c>.</summary>
    Keyboard,
}
