using Polhem.Markdown.Diagrams;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Tests.Shared;

/// <summary>
/// The node types of the model, found by reflection, and a text that contains every one of them. Each viewer's node
/// coverage test renders the text and compares the types it drew with <see cref="All"/>, so a node type added to the
/// model fails the tests of every viewer that does not draw it. The diagram in the text likewise contains every
/// <see cref="Shapes"/> type.
/// </summary>
internal static class ModelNodeTypes
{
    public static IReadOnlySet<Type> All { get; } = typeof(MarkdownNode).Assembly.GetTypes()
        .Where(type => type.IsSubclassOf(typeof(MarkdownNode)) && !type.IsAbstract)
        .ToHashSet();

    /// <summary>The shape types a diagram drawing is made of.</summary>
    public static IReadOnlySet<Type> Shapes { get; } = typeof(DiagramShape).Assembly.GetTypes()
        .Where(type => type.IsSubclassOf(typeof(DiagramShape)) && !type.IsAbstract)
        .ToHashSet();

    /// <summary>The contents of AllNodes.md, which is copied next to each test assembly.</summary>
    public static string AllNodesMarkdown() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "AllNodes.md"));
}
