using Microsoft.AspNetCore.Components;

namespace Polhem.Markdown.Blazor;

public partial class MarkdownViewer
{
    private bool _parsedStreaming;

    /// <summary>
    /// Gets or sets whether the text is still arriving, as a language model's reply does. While it is, the syntax its
    /// last block leaves open shows as it will once the text is complete: emphasis and code spans are closed, a link
    /// shows as its text until its address ends, an image or a tag still being typed waits, and a table's header row
    /// shows as a table. Set it back to <see langword="false"/> when the text is complete, and the text shows exactly as
    /// written.
    /// </summary>
    /// <remarks>The Avalonia viewer's <c>IsStreaming</c> closes the same syntax the same way.</remarks>
    [Parameter]
    public bool IsStreaming { get; set; }

    /// <summary>The text to parse: the text as written, or, while it streams, with its open syntax closed.</summary>
    private string TextToParse => IsStreaming ? UnfinishedMarkdown.Complete(Markdown ?? "") : Markdown ?? "";
}
