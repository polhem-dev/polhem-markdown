using Avalonia.Controls;
using Avalonia.LogicalTree;
using Avalonia.Threading;

namespace Polhem.Markdown.Avalonia.UnitTests;

internal static class ViewerHost
{
    /// <summary>Shows a viewer in a window, so it has a top level, and lays it out.</summary>
    public static (Window Window, MarkdownViewer Viewer) Show(string markdown, MarkdownOptions? options = null)
    {
        var viewer = new MarkdownViewer { Options = options, Markdown = markdown };
        var window = new Window { Width = 600, Height = 800, Content = viewer };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, viewer);
    }

    public static IEnumerable<Control> Blocks(MarkdownViewer viewer) =>
        ((StackPanel)viewer.GetLogicalChildren().Single()).Children;

    public static IEnumerable<T> Descendants<T>(Control root) where T : Control =>
        root.GetLogicalDescendants().OfType<T>();
}
