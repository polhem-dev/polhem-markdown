using Markdig;
using Markdig.Extensions.EmphasisExtras;
using Markdig.Syntax;
using Polhem.Markdown.Formulas;

namespace Polhem.Markdown;

/// <summary>
/// The one Markdig configuration of these packages, shared by the model the viewers render and the syntax colouring of
/// the editor, so both read a text the same way.
/// </summary>
internal static class MarkdownParser
{
    // NOTE: only the extensions whose nodes the viewers render are enabled. `UseAdvancedExtensions` would parse
    // definition lists and others that no viewer draws. Inline math follows GitHub's rules (`GitHubMathExtension`).
    // Markdig reads raw HTML into HTML blocks and inlines, which `MarkdownModelBuilder` turns into nodes (see
    // `HtmlComposer`); no HTML ever reaches a viewer as markup.
    private static readonly MarkdownPipeline s_pipeline = Pipeline();

    private static MarkdownPipeline Pipeline()
    {
        var builder = new MarkdownPipelineBuilder()
            .UsePipeTables()
            .UseAutoLinks()
            .UseCjkFriendlyEmphasis()
            .UseEmphasisExtras(EmphasisExtraOptions.Strikethrough)
            .UseTaskLists()
            .UseAlertBlocks()
            .UseEmojiAndSmiley(enableSmileys: false)
            .UseYamlFrontMatter()
            .UseFootnotes()
            .UseMathematics()
            .UsePreciseSourceLocation();
        builder.Extensions.Add(new GitHubMathExtension());
        return builder.Build();
    }

    /// <summary>
    /// Parses <paramref name="markdown"/>, or returns <see langword="null"/> when Markdig refuses it for being nested
    /// deeper than its depth limit.
    /// </summary>
    public static MarkdownDocument? Parse(string markdown)
    {
        try
        {
            return Markdig.Markdown.Parse(markdown, s_pipeline);
        }
        catch (ArgumentException)
        {
            // The text comes from people and AI models, so the callers show it as written instead of failing.
            return null;
        }
    }
}
