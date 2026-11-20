using Avalonia;
using MarkdownGallery;

AppBuilder.Configure<GalleryApp>()
    .UsePlatformDetect()
    .WithInterFont()
    .StartWithClassicDesktopLifetime(args);
