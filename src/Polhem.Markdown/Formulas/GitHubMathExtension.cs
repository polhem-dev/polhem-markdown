using Markdig;
using Markdig.Extensions.Mathematics;
using Markdig.Renderers;

namespace Polhem.Markdown.Formulas;

/// <summary>Replaces Markdig's inline math parser with <see cref="GitHubMathInlineParser"/>; add it after the math extension.</summary>
internal sealed class GitHubMathExtension : IMarkdownExtension
{
    public void Setup(MarkdownPipelineBuilder pipeline) =>
        pipeline.InlineParsers.Replace<MathInlineParser>(new GitHubMathInlineParser());

    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
        // Nothing is rendered by Markdig; the model builder reads the syntax tree.
    }
}
