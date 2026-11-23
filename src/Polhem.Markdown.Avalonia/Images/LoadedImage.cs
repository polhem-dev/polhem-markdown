using Avalonia;
using Avalonia.Media;

namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// A decoded image, the size it is shown at before it is scaled down to fit, and the bytes its pixels take, which count
/// against the viewer's image budget.
/// </summary>
internal sealed record LoadedImage(IImage Image, Size Size, long Bytes);
