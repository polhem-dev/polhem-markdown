using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Semi.Avalonia;

namespace MarkdownGallery;

/// <summary>The gallery application, on the desktop as a window and on iOS as the single view.</summary>
public sealed class GalleryApp : Application
{
    public override void Initialize() => Styles.Add(new SemiTheme());

    public override void OnFrameworkInitializationCompleted()
    {
        switch (ApplicationLifetime)
        {
            case IClassicDesktopStyleApplicationLifetime desktop:
                desktop.MainWindow = new Window { Title = "Polhem.Markdown Gallery", Width = 900, Height = 900, Content = new GalleryView() };
                break;
            case ISingleViewApplicationLifetime singleView:
                singleView.MainView = new GalleryView();
                break;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
