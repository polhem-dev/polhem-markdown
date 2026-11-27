using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Polhem.Markdown;
using Polhem.Markdown.Avalonia;

namespace MarkdownGallery;

/// <summary>
/// Shows a change to the sample text in the editor before it is made: the text it removes struck through, the text it
/// adds highlighted, and Accept and Reject buttons under the change, which follow it as the editor scrolls.
/// </summary>
/// <remarks>
/// The marks move with the text, so the user can keep editing while the change is shown; accepting or rejecting uses
/// where the marks are then.
/// </remarks>
internal sealed class DiffPreview
{
    private const string Before = "It has *three* sections and uses `ft_order` as its table.";
    private const string After = "It has *four* sections, with a new one for delivery, and uses `ft_order` as its table.";

    private static readonly IBrush s_removedBackground = new SolidColorBrush(Color.FromArgb(0x33, 0xE5, 0x48, 0x4D));
    private static readonly IBrush s_removedLine = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D));
    private static readonly IBrush s_added = new SolidColorBrush(Color.FromArgb(0x44, 0x22, 0xA3, 0x5A));

    private readonly MarkdownEditor _editor;
    private readonly Canvas _overlay;
    private readonly Border _popup;
    private MarkdownRangeMark? _removed;
    private MarkdownRangeMark? _added;

    /// <param name="editor">The editor the change is shown in.</param>
    /// <param name="overlay">A canvas over the editor, where the buttons go.</param>
    public DiffPreview(MarkdownEditor editor, Canvas overlay)
    {
        _editor = editor;
        _overlay = overlay;

        var accept = new Button { Content = "Accept" };
        accept.Click += (_, _) => Accept();
        var reject = new Button { Content = "Reject" };
        reject.Click += (_, _) => Reject();
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
    public bool IsShown => _removed is not null;

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

        // The new sentence goes after the old one, as a diff reads; its leading space is part of what it adds.
        var end = start + Before.Length;
        _editor.Replace(end, 0, " " + After);
        _editor.AddMark(start, Before.Length, MarkdownRangeMarkStyle.Background, s_removedBackground, this);
        _removed = _editor.AddMark(start, Before.Length, MarkdownRangeMarkStyle.Strikethrough, s_removedLine, this);
        _added = _editor.AddMark(end, After.Length + 1, MarkdownRangeMarkStyle.Background, s_added, this);
        _editor.Select(end + 1, 0);
        Place();
        return true;
    }

    private void Accept()
    {
        var replacements = new List<MarkdownReplacement>();
        if (_removed is { IsRemoved: false } removed)
        {
            replacements.Add(new MarkdownReplacement(removed.Start, removed.Length, ""));
        }
        if (_added is { IsRemoved: false } added && _editor.Markdown![added.Start] == ' ')
        {
            replacements.Add(new MarkdownReplacement(added.Start, 1, ""));
        }
        _editor.Replace(replacements);
        Close();
        _editor.Focus();
    }

    private void Reject()
    {
        if (_added is { IsRemoved: false } added)
        {
            _editor.Replace(added.Start, added.Length, "");
        }
        Close();
        _editor.Focus();
    }

    private void Close()
    {
        _editor.ClearMarks(this);
        _removed = null;
        _added = null;
        _popup.IsVisible = false;
    }

    /// <summary>Puts the buttons under the added text, or hides them while it is out of view.</summary>
    private void Place()
    {
        if (_added is null)
        {
            return;
        }
        if (_added.IsRemoved && _removed!.IsRemoved)
        {
            // The user deleted all of the change's text, so there is nothing left to accept or reject.
            Close();
            return;
        }
        var target = _added.IsRemoved ? _removed : _added;
        var bounds = _editor.GetTextBounds(target!.Start, target.Length);
        if (bounds is not { } box || _editor.TranslatePoint(box.BottomLeft, _overlay) is not { } point)
        {
            _popup.IsVisible = false;
            return;
        }
        Canvas.SetLeft(_popup, Math.Max(4, point.X));
        Canvas.SetTop(_popup, point.Y + 4);
        _popup.IsVisible = true;
    }
}
