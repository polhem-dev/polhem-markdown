using Polhem.Markdown.Model;

namespace Polhem.Markdown.Html;

/// <summary>An element an <see cref="HtmlComposer"/> has opened and not yet closed, with what it holds so far.</summary>
internal sealed class HtmlFrame(HtmlToken? tag)
{
    /// <summary>The element's name; empty for the root, which holds what no element does.</summary>
    public string Name { get; } = tag?.Name ?? "";

    /// <summary>The start tag, or <see langword="null"/> for the root.</summary>
    public HtmlToken? Tag { get; } = tag;

    /// <summary>The nodes inside the element, in order: inlines, and in a block context blocks too.</summary>
    public List<MarkdownNode> Children { get; } = [];

    /// <summary>For a <c>details</c>, the content of its <c>summary</c> once that has closed.</summary>
    public List<InlineNode>? Summary { get; set; }

    /// <summary>For a <c>picture</c>, the <c>srcset</c> of its <c>source</c> for a dark theme.</summary>
    public string? DarkSourceSet { get; set; }

    /// <summary>For a <c>picture</c>, the <c>srcset</c> of its <c>source</c> for a light theme.</summary>
    public string? LightSourceSet { get; set; }

    public bool IsDiscarded => HtmlElements.IsDiscarded(Name);
}
