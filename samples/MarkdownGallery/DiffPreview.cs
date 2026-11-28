using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Markdown.Avalonia;

namespace MarkdownGallery;

/// <summary>
/// Shows a change to the sample text in the editor before it is made, with <see cref="MarkdownEditor.PreviewReplace"/>:
/// the text it removes struck through, the text it adds highlighted, and Accept and Reject buttons under the change, which
/// follow it as the editor scrolls.
/// </summary>
/// <remarks>
/// The user can keep editing elsewhere while the change is shown. Accepting leaves one step to undo, and rejecting none.
/// </remarks>
internal sealed class DiffPreview
{
    private const string Before = "*three* sections";
    private const string After = "*four* sections, with a new one for delivery,";

    private readonly MarkdownEditor _editor;
    private readonly Canvas _overlay;
    private readonly Border _popup;
    private MarkdownReplacementPreview? _preview;

    /// <param name="editor">The editor the change is shown in.</param>
    /// <param name="overlay">A canvas over the editor, where the buttons go.</param>
    public DiffPreview(MarkdownEditor editor, Canvas overlay)
    {
        _editor = editor;
        _overlay = overlay;

        var accept = new Button { Content = "Accept" };
        accept.Click += (_, _) => Resolve(accept: true);
        var reject = new Button { Content = "Reject" };
        reject.Click += (_, _) => Resolve(accept: false);
        _popup = new Border
        {
            Child = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { accept, reject } },
            Padding = new Thickness(6),
            CornerRadius = new CornerRadius(6),
            BorderThickness = new Thickness(1),
            BoxShadow = BoxShadows.Parse("0 2 8 0 #40000000"),
            IsVisible = false,
        };
        _popup.Bind(Border.BackgroundProperty, _popup.GetResourceObservable("SemiColorBackground2"));
        _popup.Bind(Border.BorderBrushProperty, _popup.GetResourceObservable("SemiColorBorder"));
        overlay.Children.Add(_popup);

        // The buttons follow the change when the editor scrolls, wraps its lines again, or is resized.
        editor.ViewChanged += (_, _) => Place();
    }

    /// <summary>Whether a change is shown and waits to be accepted or rejected.</summary>
    public bool IsShown => _preview is { IsPending: true };

    /// <summary>Shows the change, unless one is shown already.</summary>
    /// <returns>Whether the change is shown: <see langword="false"/> when the text it changes is no longer there.</returns>
    public bool Show()
    {
        if (IsShown)
        {
            return true;
        }
        var start = _editor.Markdown?.IndexOf(Before, StringComparison.Ordinal) ?? -1;
        if (start < 0)
        {
            return false;
        }
        _preview = _editor.PreviewReplace(start, Before.Length, After);

        // The buttons hide when the preview ends, and come back when the user redoes the step that undid it.
        _preview.PendingChanged += (_, _) => Place();
        _editor.Select(_preview.ProposedStart + After.Length, 0);
        Place();
        return true;
    }

    private void Resolve(bool accept)
    {
        if (accept)
        {
            _preview?.Accept();
        }
        else
        {
            _preview?.Reject();
        }
        _editor.Focus();
    }

    /// <summary>Puts the buttons under the proposed text, or hides them while it is out of view or not shown.</summary>
    private void Place()
    {
        if (_preview is not { IsPending: true } preview
            || _editor.GetTextBounds(preview.ProposedStart, preview.Text.Length) is not { } box
            || _editor.TranslatePoint(box.BottomLeft, _overlay) is not { } point)
        {
            _popup.IsVisible = false;
            return;
        }
        Canvas.SetLeft(_popup, Math.Max(4, point.X));
        Canvas.SetTop(_popup, point.Y + 4);
        _popup.IsVisible = true;
    }
}
