using Polhem.Markdown.Diagrams;
using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Diagrams;

public class MermaidBlockTests
{
    private static string Fence(string code, string language = "mermaid") => "```" + language + "\n" + code + "\n```";

    [Theory(DisplayName = "A mermaid block in a supported type becomes a diagram, whatever the case of the language")]
    [InlineData("mermaid")]
    [InlineData("Mermaid")]
    public void Build_SupportedDiagram_BecomesDiagram(string language)
    {
        var markdown = Fence("flowchart TD\n  A --> B", language);

        var diagram = Single<DiagramNode>(Parse(markdown));

        Assert.Equal(new SourceRange(0, markdown.Length), diagram.Range);
    }

    [Fact(DisplayName = "With diagrams turned off a mermaid block stays code")]
    public void Build_DiagramsOff_StaysCode()
    {
        var code = Single<CodeBlockNode>(Parse(Fence("flowchart TD\n  A --> B"), new MarkdownOptions { RenderMermaidDiagrams = false }));

        Assert.Equal("mermaid", code.Language);
    }

    [Theory(DisplayName = "A diagram of another type, or with syntax outside the supported subset, stays code unchanged")]
    [InlineData("pie\n  \"A\" : 1")]
    [InlineData("classDiagram\n  A <|-- B")]
    [InlineData("")]
    [InlineData("flowchart XY\n  A --> B")]
    [InlineData("flowchart TD\n  subgraph one\n    A --> B\n  end")]
    [InlineData("flowchart TD\n  A --> B\n  classDef red fill:#f00")]
    [InlineData("flowchart TD\n  A --> B\n  style A fill:#f00")]
    [InlineData("flowchart TD\n  A --o B")]
    [InlineData("flowchart TD\n  A --x B")]
    [InlineData("flowchart TD\n  A <--> B")]
    [InlineData("flowchart TD\n  A & B --> C")]
    [InlineData("flowchart TD\n  A[(Database)]")]
    [InlineData("flowchart TD\n  A>Flag]")]
    [InlineData("flowchart TD\n  A:::warn --> B")]
    [InlineData("flowchart TD\n  A[unclosed --> B")]
    [InlineData("sequenceDiagram\n  A->>B: hi\n  loop every minute\n    B->>A: tick\n  end")]
    [InlineData("sequenceDiagram\n  A->>+B: hi")]
    [InlineData("sequenceDiagram\n  A-xB: lost")]
    [InlineData("sequenceDiagram\n  autonumber\n  A->>B: hi")]
    [InlineData("sequenceDiagram\n  activate A")]
    [InlineData("---\ntitle: x\n---\nflowchart TD\n  A --> B")]
    public void Build_Unsupported_StaysCode(string diagram)
    {
        var code = Single<CodeBlockNode>(Parse(Fence(diagram)));

        Assert.Equal("mermaid", code.Language);
        Assert.Equal(diagram, code.Code);
    }

    [Fact(DisplayName = "Comment lines are skipped and do not make a diagram fall back")]
    public void Build_Comments_AreSkipped()
    {
        Single<DiagramNode>(Parse(Fence("%% before\nflowchart TD\n  %% inside\n  A --> B")));
    }

    [Fact(DisplayName = "A chart with more nodes than the limit stays code")]
    public void Build_TooManyNodes_StaysCode()
    {
        var nodes = string.Join("\n", Enumerable.Range(0, FlowchartParser.MaxNodes + 1).Select(index => "  N" + index));

        Single<CodeBlockNode>(Parse(Fence("flowchart TD\n" + nodes)));
    }

    [Fact(DisplayName = "A diagram with a line longer than the limit stays code")]
    public void Build_LongLine_StaysCode()
    {
        var line = "  A -- " + new string('x', MermaidDiagram.MaxLineLength) + " --> B";

        Single<CodeBlockNode>(Parse(Fence("flowchart TD\n" + line)));
    }

    [Fact(DisplayName = "A chart at the size limits is laid out")]
    public void Build_AtLimits_IsDrawn()
    {
        var links = Enumerable.Range(0, FlowchartParser.MaxEdges)
            .Select(index => $"  N{index % FlowchartParser.MaxNodes} --> N{(index * 7 + 3) % FlowchartParser.MaxNodes}");

        Single<DiagramNode>(Parse(Fence("flowchart TD\n" + string.Join("\n", links))));
    }

    [Fact(DisplayName = "Any other fenced block is not affected")]
    public void Build_OtherLanguage_StaysCode()
    {
        Assert.Equal("text", Single<CodeBlockNode>(Parse(Fence("flowchart TD\n  A --> B", "text"))).Language);
    }
}
