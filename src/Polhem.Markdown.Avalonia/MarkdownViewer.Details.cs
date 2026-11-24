using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>The sections the reader opened or closed, by <see cref="DetailsKey"/>, while the text shows them.</summary>
    private readonly Dictionary<string, bool> _detailsOpen = new(StringComparer.Ordinal);

    /// <summary>
    /// Identifies a section across rebuilds: where it starts and its summary. A reply that is still arriving changes the
    /// section's content and end, never these.
    /// </summary>
    internal static string DetailsKey(DetailsNode details) =>
        details.Range.Start.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0"
        + string.Concat(details.Summary.SelectMany(NodeTree.SelfAndDescendants).OfType<TextNode>().Select(text => text.Text));

    /// <summary>Whether a section shows open: as the reader left it, or else as the text says.</summary>
    internal bool IsDetailsOpen(string key, bool written) => _detailsOpen.GetValueOrDefault(key, written);

    internal void RememberDetails(string key, bool open) => _detailsOpen[key] = open;

    /// <summary>Forgets the sections the text no longer shows, so a new text starts as it is written.</summary>
    private void ForgetDetailsNotIn(DocumentNode document)
    {
        if (_detailsOpen.Count == 0)
        {
            return;
        }
        var shown = document.Blocks.SelectMany(NodeTree.SelfAndDescendants).OfType<DetailsNode>().Select(DetailsKey).ToHashSet(StringComparer.Ordinal);
        foreach (var key in _detailsOpen.Keys.Where(key => !shown.Contains(key)).ToList())
        {
            _detailsOpen.Remove(key);
        }
    }
}
