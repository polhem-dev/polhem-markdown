namespace Polhem.Markdown.Html;

/// <summary>
/// A piece of HTML: text, or a tag. <see cref="Start"/> and <see cref="Length"/> locate it in the text that was
/// tokenized. A tag's <see cref="Name"/> and attribute names are lower case, and its attribute values are decoded; a
/// text token's <see cref="Text"/> is decoded.
/// </summary>
internal sealed record HtmlToken(
    HtmlTokenKind Kind,
    int Start,
    int Length,
    string Name,
    string Text,
    IReadOnlyDictionary<string, string> Attributes)
{
    public int End => Start + Length;

    /// <summary>The value of attribute <paramref name="name"/>, or <see langword="null"/> when the tag has none.</summary>
    public string? Attribute(string name) => Attributes.GetValueOrDefault(name);
}
