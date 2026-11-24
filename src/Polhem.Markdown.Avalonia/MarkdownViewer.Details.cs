using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

public partial class MarkdownViewer
{
    /// <summary>The sections the reader opened or closed, by key, while the text shows a section with that key.</summary>
    private readonly Dictionary<string, bool> _detailsOpen = [];

    /// <summary>
    /// The key of each section of the text being shown; see <see cref="AssignDetailsKeys"/>. Records compare their lists
    /// of children by reference, so two sections are equal only when they are the same node.
    /// </summary>
    private readonly Dictionary<DetailsNode, string> _detailsKeys = [];

    /// <summary>
    /// Gives each section a key from its summary and its place among the sections with the same summary. A reply that is
    /// still arriving, and an edit above a section, change neither, so the section stays as the reader left it. A new
    /// text with a section of the same summary in the same place takes the reader's choice too.
    /// </summary>
    private void AssignDetailsKeys(DocumentNode document)
    {
        _detailsKeys.Clear();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var details in document.Blocks.SelectMany(NodeTree.SelfAndDescendants).OfType<DetailsNode>())
        {
            var summary = string.Concat(details.Summary.SelectMany(NodeTree.SelfAndDescendants).OfType<TextNode>().Select(text => text.Text));
            var count = seen.GetValueOrDefault(summary);
            seen[summary] = count + 1;
            _detailsKeys[details] = summary + "\0" + count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        // Forget the choices for sections the text no longer shows.
        var shown = _detailsKeys.Values.ToHashSet(StringComparer.Ordinal);
        foreach (var key in _detailsOpen.Keys.Where(key => !shown.Contains(key)).ToList())
        {
            _detailsOpen.Remove(key);
        }
    }

    internal string DetailsKey(DetailsNode details) => _detailsKeys.GetValueOrDefault(details, "");

    /// <summary>Whether a section shows open: as the reader left it, or else as the text says.</summary>
    internal bool IsDetailsOpen(string key, bool written) => _detailsOpen.GetValueOrDefault(key, written);

    internal void RememberDetails(string key, bool open) => _detailsOpen[key] = open;
}
