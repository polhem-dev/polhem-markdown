using Avalonia;
using Avalonia.Input;
using Avalonia.Interactivity;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Avalonia;

// Where the text of the source shows, for a host that keeps an editor and the viewer in step.
public partial class MarkdownViewer
{
    /// <summary>Defines the <see cref="SourceRequested"/> event, which bubbles.</summary>
    public static readonly RoutedEvent<MarkdownSourceRequestedEventArgs> SourceRequestedEvent =
        RoutedEvent.Register<MarkdownViewer, MarkdownSourceRequestedEventArgs>(nameof(SourceRequested), RoutingStrategies.Bubble);

    /// <summary>Where each shown block comes from in the source, in the order of <see cref="_panel"/>'s children.</summary>
    private readonly List<SourceRange> _ranges = [];

    /// <summary>
    /// Occurs when a user clicks the text with the platform's command key held (Ctrl, or Cmd on macOS), to go to where it
    /// comes from in the Markdown source. A handler that marks it handled also keeps the click from doing anything else,
    /// such as opening a link.
    /// </summary>
    public event EventHandler<MarkdownSourceRequestedEventArgs>? SourceRequested
    {
        add => AddHandler(SourceRequestedEvent, value);
        remove => RemoveHandler(SourceRequestedEvent, value);
    }

    /// <summary>
    /// Gets where the text at <paramref name="sourceOffset"/> in <see cref="Markdown"/> shows: a vertical position in the
    /// viewer's own coordinates, or <see langword="null"/> when the viewer shows nothing or has not been laid out.
    /// </summary>
    /// <remarks>
    /// The position is that of the top-level block the offset falls in, such as a paragraph, a list or a table, moved down
    /// in proportion to how far into the block's source the offset is. Text between blocks belongs to the block before
    /// it. A host that scrolls the viewer adds the viewer's position in the scrolled content.
    /// </remarks>
    /// <param name="sourceOffset">An offset in <see cref="Markdown"/>, in UTF-16 code units.</param>
    public double? GetVerticalOffset(int sourceOffset)
    {
        if (!HasLayout())
        {
            return null;
        }
        var index = BlockAtSource(sourceOffset);
        var range = _ranges[index];
        var bounds = _panel.Children[index].Bounds;
        var fraction = range.Length == 0 ? 0 : Math.Clamp((sourceOffset - range.Start) / (double)range.Length, 0, 1);
        return bounds.Top + fraction * bounds.Height;
    }

    /// <summary>
    /// Gets the offset in <see cref="Markdown"/> of the text shown at <paramref name="verticalOffset"/>, a vertical
    /// position in the viewer's own coordinates, or <see langword="null"/> when the viewer shows nothing or has not been
    /// laid out.
    /// </summary>
    /// <remarks>
    /// The inverse of <see cref="GetVerticalOffset"/>: the block at that position, and the offset as far into its source as
    /// the position is into the block. Above the first block gives its start, below the last its end.
    /// </remarks>
    /// <param name="verticalOffset">A vertical position in the viewer's own coordinates.</param>
    public int? GetSourceOffset(double verticalOffset)
    {
        if (!HasLayout())
        {
            return null;
        }
        var index = BlockAtPosition(verticalOffset);
        var range = _ranges[index];
        var bounds = _panel.Children[index].Bounds;
        var fraction = bounds.Height <= 0 ? 0 : Math.Clamp((verticalOffset - bounds.Top) / bounds.Height, 0, 1);
        return range.Start + (int)Math.Round(fraction * range.Length);
    }

    /// <summary>Keeps the source range of every block shown, after a rebuild.</summary>
    private void KeepRanges(DocumentNode document)
    {
        _ranges.Clear();
        _ranges.AddRange(document.Blocks.Select(block => block.Range));
    }

    private bool HasLayout() => _ranges.Count > 0 && _ranges.Count == _panel.Children.Count && _panel.Bounds.Height > 0;

    /// <summary>The block whose source holds <paramref name="sourceOffset"/>, or the last one that starts before it.</summary>
    private int BlockAtSource(int sourceOffset)
    {
        int low = 0, high = _ranges.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_ranges[middle].Start <= sourceOffset)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        return low;
    }

    /// <summary>The block at <paramref name="verticalOffset"/>, or the last one that starts above it.</summary>
    private int BlockAtPosition(double verticalOffset)
    {
        int low = 0, high = _panel.Children.Count - 1;
        while (low < high)
        {
            var middle = (low + high + 1) / 2;
            if (_panel.Children[middle].Bounds.Top <= verticalOffset)
            {
                low = middle;
            }
            else
            {
                high = middle - 1;
            }
        }
        return low;
    }

    /// <summary>
    /// Raises <see cref="SourceRequestedEvent"/> for a click with the command key held, before the text under the pointer
    /// handles the click, and takes the click only when a handler handled the request.
    /// </summary>
    private void OnPointerPressedForSource(object? sender, PointerPressedEventArgs e)
    {
        var commandKey = Application.Current?.PlatformSettings?.HotkeyConfiguration.CommandModifiers ?? KeyModifiers.Control;
        if (e.KeyModifiers != commandKey || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed
            || GetSourceOffset(e.GetPosition(this).Y) is not { } sourceOffset)
        {
            return;
        }
        var args = new MarkdownSourceRequestedEventArgs(sourceOffset);
        RaiseEvent(args);
        e.Handled = args.Handled;
    }
}
