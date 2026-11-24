using Avalonia.Controls;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Styling;

namespace Polhem.Markdown.Avalonia;

/// <summary>
/// The look of the editor's AvaloniaEdit parts, whatever theme the host uses. AvaloniaEdit's own Fluent and Simple themes
/// map a few resources to resources of Avalonia's Fluent and Simple themes, and a host on another theme (Semi, Material)
/// lacks them; its Simple theme fails with <see cref="KeyNotFoundException"/> there. This dictionary takes AvaloniaEdit's
/// templates and gives those resources values of its own, so the editor needs nothing from the host.
/// </summary>
/// <remarks>
/// It is built in code rather than XAML because an assembly with compiled XAML exports the public types the XAML compiler
/// generates in the <c>CompiledAvaloniaXaml</c> namespace (polhem-dev/polhem-markdown#48).
/// <c>PublicApiGateTests.ExportedTypes_EachPackage_AreInItsNamespace</c> fails when the package exports them again.
/// </remarks>
internal sealed class EditorTheme : ResourceDictionary
{
    private static readonly Uri s_editorTemplates = new("avares://AvaloniaEdit/Themes/Base.xaml");

    public EditorTheme()
    {
        this["SearchPanelFontSize"] = 13d;
        this["SearchPanelFontFamily"] = FontFamily.Default;

        ThemeDictionaries[ThemeVariant.Default] = Variant(background: 0xFFF3F4F6, border: 0xFFD1D5DB);
        ThemeDictionaries[ThemeVariant.Dark] = Variant(background: 0xFF2B2D31, border: 0xFF4B5563);

        MergedDictionaries.Add(new ResourceInclude((Uri?)null) { Source = s_editorTemplates });
    }

    private static ResourceDictionary Variant(uint background, uint border) => new()
    {
        ["SearchPanelBackgroundBrush"] = new SolidColorBrush(Color.FromUInt32(background)),
        ["SearchPanelBorderBrush"] = new SolidColorBrush(Color.FromUInt32(border)),
        ["TextAreaSelectionBrush"] = new SolidColorBrush(Color.FromUInt32(0x663B82F6)),
    };
}
