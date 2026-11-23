using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace Polhem.Markdown.Avalonia.UnitTests;

internal static class ViewerHost
{
    /// <summary>
    /// Shows a viewer in a window, so it has a top level, and lays it out. Images load from <paramref name="images"/>,
    /// and by default from nowhere, so no test reaches the network.
    /// </summary>
    public static (Window Window, MarkdownViewer Viewer) Show(
        string markdown, MarkdownOptions? options = null, Func<Uri, CancellationToken, Task<Stream?>>? images = null)
    {
        var viewer = new MarkdownViewer { ImageLoader = images ?? NoImages, Options = options, Markdown = markdown };
        var window = new Window { Width = 600, Height = 800, Content = viewer };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, viewer);
    }

    public static Task<Stream?> NoImages(Uri source, CancellationToken cancellationToken) => Task.FromResult<Stream?>(null);

    public static IEnumerable<Control> Blocks(MarkdownViewer viewer) =>
        ((StackPanel)viewer.GetLogicalChildren().Single()).Children;

    public static IEnumerable<T> Descendants<T>(Control root) where T : Control =>
        root.GetLogicalDescendants().OfType<T>();
}
