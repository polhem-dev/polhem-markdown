using System.Globalization;
using Polhem.Markdown.Diagrams;
using Polhem.Markdown.Formulas;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Output;

internal sealed partial class HtmlWriter
{
    private readonly HashSet<Type> _writtenShapes = [];
    private readonly HashSet<Type> _writtenFormulas = [];
    private readonly Dictionary<Uri, string> _imageSources = [];

    /// <summary>The diagram shape types written so far.</summary>
    public IReadOnlySet<Type> WrittenShapeTypes => _writtenShapes;

    /// <summary>The formula part types written so far.</summary>
    public IReadOnlySet<Type> WrittenFormulaTypes => _writtenFormulas;

    /// <summary>
    /// Writes a code block with its language above it, and each highlighted token in a span whose class names its kind.
    /// The viewer's button that copies the code is left out.
    /// </summary>
    private void CodeBlock(CodeBlockNode code)
    {
        _html.Append("<div class=\"pmd-code\"><div class=\"pmd-code-header\"><span class=\"pmd-code-lang\">");
        Text(code.Language);
        _html.Append("</span></div><pre><code>");
        var position = 0;
        foreach (var token in code.Tokens)
        {
            if (token.Start > position)
            {
                Text(code.Code[position..token.Start]);
            }
            _html.Append("<span class=\"").Append(ViewerMarkup.TokenClass(token.Kind)).Append("\">");
            Text(code.Code.Substring(token.Start, token.Length));
            _html.Append("</span>");
            position = token.End;
        }
        if (position < code.Code.Length)
        {
            Text(code.Code[position..]);
        }
        _html.Append("</code></pre></div>");
    }

    /// <summary>Writes a diagram as inline SVG at the drawing's own size, its colors from classes, as the viewer does.</summary>
    private void Diagram(DiagramNode diagram)
    {
        var drawing = diagram.Drawing;
        _html.Append("<div class=\"pmd-diagram\"><svg");
        Attribute("viewBox", "0 0 " + ViewerMarkup.Number(drawing.Width) + " " + ViewerMarkup.Number(drawing.Height));
        Attribute("width", ViewerMarkup.Number(drawing.Width));
        Attribute("height", ViewerMarkup.Number(drawing.Height));
        Attribute("font-size", ViewerMarkup.Number(DiagramDrawing.FontSize));
        Attribute("stroke-width", ViewerMarkup.Number(DiagramDrawing.OutlineThickness));
        _html.Append('>');
        foreach (var shape in drawing.Shapes)
        {
            Shape(shape);
        }
        _html.Append("</svg></div>");
    }

    private void Shape(DiagramShape shape)
    {
        switch (shape)
        {
            case DiagramRectangle rectangle:
                _html.Append("<rect class=\"pmd-d-box\"");
                Attribute("x", ViewerMarkup.Number(rectangle.X));
                Attribute("y", ViewerMarkup.Number(rectangle.Y));
                Attribute("width", ViewerMarkup.Number(rectangle.Width));
                Attribute("height", ViewerMarkup.Number(rectangle.Height));
                if (rectangle.CornerRadius > 0)
                {
                    Attribute("rx", ViewerMarkup.Number(rectangle.CornerRadius));
                }
                _html.Append("></rect>");
                break;
            case DiagramPolygon polygon:
                _html.Append("<polygon");
                Attribute("class", ViewerMarkup.PolygonClass(polygon));
                Attribute("points", ViewerMarkup.Points(polygon.Points));
                _html.Append("></polygon>");
                break;
            case DiagramLine line:
                _html.Append("<polyline");
                Attribute("class", ViewerMarkup.LineClass(line));
                Attribute("points", ViewerMarkup.Points(line.Points));
                Attribute("stroke-width", ViewerMarkup.Number(line.Thickness));
                if (line.Dash > 0)
                {
                    Attribute("stroke-dasharray", ViewerMarkup.Number(line.Dash));
                }
                _html.Append("></polyline>");
                break;
            case DiagramText text:
                _html.Append("<text");
                Attribute("x", ViewerMarkup.Number(text.X));
                Attribute("y", ViewerMarkup.Number(text.Y));
                _html.Append('>');
                Text(text.Text);
                _html.Append("</text>");
                break;
            default:
                // NOTE: a shape type is recorded only when a case above wrote it, so the shape coverage test fails for a
                // type added to the drawing that is not written here.
                return;
        }
        _writtenShapes.Add(shape.GetType());
    }

    /// <summary>Writes a formula as MathML, which the browser lays out with no script, its LaTeX source as an annotation.</summary>
    private void Formula(IFormulaItem formula, string source, bool display) =>
        _html.Append(MathMlWriter.Write(formula, source, display, _writtenFormulas));

    /// <summary>
    /// Writes an image, inside a <c>picture</c> when it has a source for a dark or a light theme, as the viewer does.
    /// Unlike the viewer's, the image is not loaded lazily, so that a page printed at once has every image in it.
    /// </summary>
    private void Image(ImageNode image)
    {
        var themed = image.DarkSource is not null || image.LightSource is not null;
        if (themed)
        {
            _html.Append("<picture>");
            ThemeSource("(prefers-color-scheme: dark)", image.DarkSource);
            ThemeSource("(prefers-color-scheme: light)", image.LightSource);
        }
        _html.Append("<img class=\"pmd-img\"");
        Attribute("src", ImageSource(image.Source));
        Attribute("alt", image.AlternativeText);
        if (image.Title is { } title)
        {
            Attribute("title", title);
        }
        _html.Append(" referrerpolicy=\"no-referrer\"");
        if (image.Width is { } width)
        {
            Attribute("width", width.ToString(CultureInfo.InvariantCulture));
        }
        if (image.Height is { } height)
        {
            Attribute("height", height.ToString(CultureInfo.InvariantCulture));

            // As in the viewer: with a width, the height bounds the image and the stylesheet lets it follow the
            // proportions.
            if (image.Width is not null)
            {
                Attribute("style", "max-height:" + height.ToString(CultureInfo.InvariantCulture) + "px");
            }
        }
        _html.Append('>');
        if (themed)
        {
            _html.Append("</picture>");
        }
    }

    private void ThemeSource(string media, Uri? source)
    {
        if (source is null)
        {
            return;
        }
        _html.Append("<source");
        Attribute("media", media);
        Attribute("srcset", ImageSource(source));
        _html.Append('>');
    }

    /// <summary>
    /// The URI an image is written with: the <c>data:</c> URI of the bytes the host returns for it, or its own. Each source
    /// is asked for once, however often the document shows it.
    /// </summary>
    private string ImageSource(Uri source)
    {
        if (_options.ImageContent is not { } content)
        {
            return source.AbsoluteUri;
        }
        if (!_imageSources.TryGetValue(source, out var written))
        {
            written = content(source)?.ToDataUri() ?? source.AbsoluteUri;
            _imageSources.Add(source, written);
        }
        return written;
    }
}
