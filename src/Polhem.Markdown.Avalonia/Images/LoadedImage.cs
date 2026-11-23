using Avalonia;
using Avalonia.Media;

namespace Polhem.Markdown.Avalonia.Images;

/// <summary>A decoded image and the size it is shown at before it is scaled down to fit.</summary>
internal sealed record LoadedImage(IImage Image, Size Size);
