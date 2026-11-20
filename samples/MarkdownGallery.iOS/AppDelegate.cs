using Avalonia;
using Avalonia.iOS;
using Foundation;

namespace MarkdownGallery.iOS;

[Register("AppDelegate")]
#pragma warning disable CA1711 // "Delegate" is the iOS naming convention for the application delegate.
public partial class AppDelegate : AvaloniaAppDelegate<GalleryApp>
#pragma warning restore CA1711
{
    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder) =>
        base.CustomizeAppBuilder(builder).WithInterFont();
}
