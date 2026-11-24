using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Polhem.Markdown.Avalonia;

/// <summary>The resources of the editor's AvaloniaEdit parts; see <c>EditorTheme.axaml</c>.</summary>
internal sealed partial class EditorTheme : ResourceDictionary
{
    public EditorTheme() => AvaloniaXamlLoader.Load(this);
}
