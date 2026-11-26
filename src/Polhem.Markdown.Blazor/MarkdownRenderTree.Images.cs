using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Polhem.Markdown.Model;

namespace Polhem.Markdown.Blazor;

internal sealed partial class MarkdownRenderTree
{
    /// <summary>How many links the inline being written is inside.</summary>
    private int _linkDepth;

    /// <summary>
    /// Writes an image, inside a <c>picture</c> when it has a source for a dark or a light theme, which the browser picks
    /// by the reader's color scheme. With <see cref="MarkdownViewer.ZoomImages"/> on, an image in no link is inside a
    /// button that opens it at full size.
    /// </summary>
    private void Image(ImageNode image)
    {
        _builder.OpenRegion(0);
        if (_viewer.ZoomImages && _linkDepth == 0)
        {
            // NOTE: a button, rather than handlers on the image, gives the keyboard and screen readers the image as
            // something to press, named by its alternative text.
            _builder.OpenElement(0, "button");
            _builder.AddAttribute(1, "type", "button");
            _builder.AddAttribute(2, ClassAttribute, "pmd-image-button");
            _builder.AddAttribute(3, OnClickAttribute, EventCallback.Factory.Create<MouseEventArgs>(_viewer, () => _viewer.ActivateImageAsync(image)));
            _builder.OpenRegion(4);
            Picture(image, zoomed: false);
            _builder.CloseRegion();
            _builder.CloseElement();
        }
        else
        {
            _builder.OpenRegion(5);
            Picture(image, zoomed: false);
            _builder.CloseRegion();
        }
        _builder.CloseRegion();
    }

    /// <summary>
    /// Writes the dialog that shows <paramref name="image"/> at full size over the page, which the script opens, zooms,
    /// pans and closes. It sits beside the viewer's root element, with its classes, so the variables a host sets on them
    /// reach it.
    /// </summary>
    public void ZoomDialog(ImageNode image, string? extraClass)
    {
        _builder.OpenElement(0, "dialog");
        _builder.AddAttribute(1, ClassAttribute, string.IsNullOrWhiteSpace(extraClass) ? "pmd pmd-zoom" : "pmd pmd-zoom " + extraClass);
        _builder.AddAttribute(2, "aria-label", image.AlternativeText);
        _builder.AddElementReferenceCapture(3, _viewer.SetZoomDialog);
        _builder.OpenRegion(4);
        Picture(image, zoomed: true);
        _builder.CloseRegion();
        _builder.OpenElement(5, "button");
        _builder.AddAttribute(6, "type", "button");
        _builder.AddAttribute(7, ClassAttribute, "pmd-zoom-close");
        _builder.AddAttribute(8, "aria-label", MarkdownViewer.ZoomCloseLabel);
        _builder.AddAttribute(9, "title", MarkdownViewer.ZoomCloseLabel);
        _builder.OpenElement(10, "svg");
        _builder.AddAttribute(11, "viewBox", "0 0 24 24");
        _builder.AddAttribute(12, "aria-hidden", "true");
        _builder.OpenElement(13, "path");
        _builder.AddAttribute(14, "d", "M6 6 18 18M18 6 6 18");
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseElement();
        _builder.CloseElement();
    }

    private void Picture(ImageNode image, bool zoomed)
    {
        if (image.DarkSource is null && image.LightSource is null)
        {
            Img(image, zoomed);
            return;
        }
        _builder.OpenElement(10, "picture");
        ThemeSource(11, "(prefers-color-scheme: dark)", image.DarkSource);
        ThemeSource(12, "(prefers-color-scheme: light)", image.LightSource);
        _builder.OpenRegion(13);
        Img(image, zoomed);
        _builder.CloseRegion();
        _builder.CloseElement();
    }

    private void ThemeSource(int sequence, string media, Uri? source)
    {
        if (source is null)
        {
            return;
        }
        _builder.OpenRegion(sequence);
        _builder.OpenElement(0, "source");
        _builder.AddAttribute(1, "media", media);
        _builder.AddAttribute(2, "srcset", source.AbsoluteUri);
        _builder.CloseElement();
        _builder.CloseRegion();
    }

    /// <summary>
    /// Writes the image itself: in the text at the size it asks for, or, <paramref name="zoomed"/>, at its own size for the
    /// script to scale.
    /// </summary>
    private void Img(ImageNode image, bool zoomed)
    {
        _builder.OpenElement(0, "img");
        _builder.AddAttribute(1, ClassAttribute, zoomed ? "pmd-zoom-image" : "pmd-img");
        _builder.AddAttribute(2, "src", image.Source.AbsoluteUri);
        _builder.AddAttribute(3, "alt", image.AlternativeText);
        if (zoomed)
        {
            _builder.AddAttribute(4, "draggable", "false");
            _builder.AddAttribute(7, "referrerpolicy", "no-referrer");
            _builder.CloseElement();
            return;
        }
        if (image.Title is { } title)
        {
            _builder.AddAttribute(5, "title", title);
        }
        _builder.AddAttribute(6, "loading", "lazy");
        _builder.AddAttribute(8, "decoding", "async");
        _builder.AddAttribute(9, "referrerpolicy", "no-referrer");
        if (image.Width is { } width)
        {
            _builder.AddAttribute(10, "width", width);
        }
        if (image.Height is { } height)
        {
            _builder.AddAttribute(11, "height", height);

            // NOTE: with a width the stylesheet lets the height follow the image's proportions, so a narrow viewer
            // shrinks both; the height still bounds it, and the image fits inside both as in the Avalonia viewer.
            if (image.Width is not null)
            {
                _builder.AddAttribute(12, "style", "max-height:" + height.ToString(CultureInfo.InvariantCulture) + "px");
            }
        }
        _builder.CloseElement();
    }
}
