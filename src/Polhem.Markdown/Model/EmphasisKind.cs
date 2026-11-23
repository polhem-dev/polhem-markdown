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
}
