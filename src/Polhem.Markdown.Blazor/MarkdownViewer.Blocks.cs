using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

// Which top-level blocks render again when the text or a setting changes.
public partial class MarkdownViewer
{
    /// <summary>
    /// The key of each top-level block of the text parsed last, with how many blocks before it have the same key, which
    /// together tell the blocks apart for Blazor.
    /// </summary>
    private (string Key, int Occurrence)[] _blockKeys = [];

    private readonly HashSet<MarkdownBlock> _shownBlocks = [];
    private int _generation;
    private (MarkdownOptions? Options, string CopyCodeLabel, bool HandlesLinks, bool ZoomImages, string Page)? _blockSettings;

    /// <summary>How many times a top-level block has rendered, for tests.</summary>
    internal int BlockRenderCount { get; set; }

    /// <summary>The node types the blocks shown drew, for the node coverage test.</summary>
    internal IReadOnlySet<Type> RenderedNodeTypes => _shownBlocks.SelectMany(block => block.RenderedNodeTypes).ToHashSet();

    /// <summary>The diagram shape types the blocks shown drew, for the shape coverage test.</summary>
    internal IReadOnlySet<Type> RenderedShapeTypes => _shownBlocks.SelectMany(block => block.RenderedShapeTypes).ToHashSet();

    /// <summary>The formula part types the blocks shown wrote, for the formula coverage test.</summary>
    internal IReadOnlySet<Type> RenderedFormulaTypes => _shownBlocks.SelectMany(block => block.RenderedFormulaTypes).ToHashSet();

    internal void AddBlock(MarkdownBlock block) => _shownBlocks.Add(block);

    internal void RemoveBlock(MarkdownBlock block) => _shownBlocks.Remove(block);

    /// <summary>
    /// A number that changes when a setting that blocks show changes, which renders every block again: the options, the
    /// copy button's label, whether the host handles links, whether images open at full size, and the page that links
    /// to headings name.
    /// </summary>
    private int BlockGeneration()
    {
        var settings = (_parsedOptions, CopyCodeLabel, HandlesLinks, ZoomImages, AnchorHref(""));
        if (_blockSettings != settings)
        {
            _blockSettings = settings;
            _generation++;
        }
        return _generation;
    }

    /// <summary>
    /// The key of each top-level block: its source, followed by what inside it depends on the rest of the text (what
    /// <see cref="NodeTree.TextDependentKey"/> names, and image sources). A block that appears more than once has its
    /// occurrence counted, since Blazor needs the keys of siblings to differ.
    /// </summary>
    /// <remarks>
    /// NOTE: as in the Avalonia viewer, a block is kept when its own source is unchanged, so a link that refers to a
    /// reference definition elsewhere in the text keeps its old target when only that definition changes; passing new
    /// options renders every block again.
    /// </remarks>
    private static (string Key, int Occurrence)[] BlockKeys(string markdown, DocumentNode document)
    {
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        var keys = new (string Key, int Occurrence)[document.Blocks.Count];
        for (var index = 0; index < keys.Length; index++)
        {
            var block = document.Blocks[index];
            var parts = NodeTree.SelfAndDescendants(block)
                .Select(node => NodeTree.TextDependentKey(node) ?? node switch
                {
                    // An image written by reference (`![a][r]`) takes its sources from a definition elsewhere in the text.
                    ImageNode image => "!" + image.Source.AbsoluteUri + "\0" + image.DarkSource?.AbsoluteUri + "\0" + image.LightSource?.AbsoluteUri,
                    _ => null,
                })
                .OfType<string>()
                .Prepend(markdown.Substring(block.Range.Start, block.Range.Length));
            var key = string.Join("\0", parts);
            var occurrence = occurrences.GetValueOrDefault(key);
            occurrences[key] = occurrence + 1;
            keys[index] = (key, occurrence);
        }
        return keys;
    }
}
