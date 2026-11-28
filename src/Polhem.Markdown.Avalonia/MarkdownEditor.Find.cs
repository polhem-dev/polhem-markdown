using Avalonia;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;

namespace Polhem.Markdown.Avalonia;

// Find and replace in the source (polhem-dev/polhem-markdown#97): the matches, their highlights, and replacing them.
public partial class MarkdownEditor
{
    /// <summary>Defines the <see cref="FindMatchBrush"/> property, shared with <see cref="MarkdownViewer"/> and its default.</summary>
    public static readonly StyledProperty<IBrush> FindMatchBrushProperty = MarkdownViewer.FindMatchBrushProperty.AddOwner<MarkdownEditor>();

    /// <summary>Defines the <see cref="FindCurrentMatchBrush"/> property, shared with <see cref="MarkdownViewer"/> and its default.</summary>
    public static readonly StyledProperty<IBrush> FindCurrentMatchBrushProperty = MarkdownViewer.FindCurrentMatchBrushProperty.AddOwner<MarkdownEditor>();

    /// <summary>The highlights of the matches, drawn apart from the host's marks so <see cref="ClearMarks()"/> leaves them.</summary>
    private readonly TextSegmentCollection<RangeMarkSegment> _findHighlights = [];

    private readonly List<(int Start, int Length)> _findMatches = [];
    private FindBar? _findBar;
    private TextFinder? _finder;
    private string? _findQuery;
    private MarkdownFindOptions _findOptions;
    private int _findCurrent = -1;

    /// <summary>Gets or sets the brush behind the matches of a find.</summary>
    public IBrush FindMatchBrush
    {
        get => GetValue(FindMatchBrushProperty);
        set => SetValue(FindMatchBrushProperty, value);
    }

    /// <summary>Gets or sets the brush behind the current match of a find, which is also selected.</summary>
    public IBrush FindCurrentMatchBrush
    {
        get => GetValue(FindCurrentMatchBrushProperty);
        set => SetValue(FindCurrentMatchBrushProperty, value);
    }

    /// <summary>Gets the number of matches of the current find, or 0.</summary>
    public int FindMatchCount => _findMatches.Count;

    /// <summary>
    /// Gets the position of the current match among the matches, from 1, or 0 when there is none, as after an edit that
    /// moved the selection off the match.
    /// </summary>
    public int CurrentFindMatch => _findCurrent + 1;

    /// <summary>The matches as they are highlighted, for tests.</summary>
    internal IReadOnlyList<MarkdownRangeMark> FindHighlights => [.. _findHighlights.Select(segment => segment.Mark)];

    /// <summary>
    /// Finds <paramref name="text"/> in the source, highlights every match, and selects the first one at or after the
    /// selection, or the first one in the text, scrolling it into view. The highlights follow the edits until
    /// <see cref="CloseFind"/>. When the find bar is open, it shows the text and the options.
    /// </summary>
    /// <param name="text">The text to find. An empty text finds nothing.</param>
    /// <param name="options">How to find it.</param>
    /// <returns>The number of matches.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="text"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="options"/> has <see cref="MarkdownFindOptions.RegularExpression"/> and <paramref name="text"/> is
    /// not a valid regular expression.
    /// </exception>
    public int Find(string text, MarkdownFindOptions options = MarkdownFindOptions.None)
    {
        ArgumentNullException.ThrowIfNull(text);
        var finder = new TextFinder(text, options);
        if (_findBar is not null)
        {
            _findBar.SetQuery(text, options);
            _findBar.ShowInvalid(false);
        }
        Run(finder, text, options);
        return _findMatches.Count;
    }

    /// <summary>Selects the next match, after the last one the first. Returns whether there is a match.</summary>
    /// <remarks>With no match current, as after an edit, it is the first match after the selection.</remarks>
    public bool FindNext() => Step(1);

    /// <summary>Selects the previous match, before the first one the last. Returns whether there is a match.</summary>
    /// <remarks>With no match current, as after an edit, it is the last match before the selection.</remarks>
    public bool FindPrevious() => Step(-1);

    /// <summary>
    /// Replaces the current match with <paramref name="replacement"/>, as one step to undo, and selects the next match.
    /// </summary>
    /// <remarks>
    /// With no match current, it selects the next match instead and replaces nothing, so the user sees what the next press
    /// replaces. Nothing changes when the editor is read-only. With <see cref="MarkdownFindOptions.RegularExpression"/>,
    /// <c>$1</c> or <c>${name}</c> in <paramref name="replacement"/> is the text of that group of the match.
    /// </remarks>
    /// <param name="replacement">The text that takes the match's place: empty to delete it.</param>
    /// <returns>Whether a match was replaced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="replacement"/> is <see langword="null"/>.</exception>
    public bool ReplaceCurrentMatch(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (IsReadOnly || _finder is null || _findMatches.Count == 0)
        {
            return false;
        }
        if (_findCurrent < 0)
        {
            Step(1);
            return false;
        }
        var match = _findMatches[_findCurrent];
        var text = _finder.Replacement(_editor.Document.Text, match, replacement);
        Replace(match.Start, match.Length, text);
        if (_findMatches.Count > 0)
        {
            var after = match.Start + text.Length;
            var next = _findMatches.FindIndex(found => found.Start >= after);
            _findCurrent = next >= 0 ? next : 0;
        }
        ShowMatches(select: true);
        return true;
    }

    /// <summary>Replaces every match with <paramref name="replacement"/>, as one step to undo.</summary>
    /// <remarks>
    /// Nothing changes when the editor is read-only. With <see cref="MarkdownFindOptions.RegularExpression"/>, <c>$1</c>
    /// or <c>${name}</c> in <paramref name="replacement"/> is the text of that group of each match.
    /// </remarks>
    /// <param name="replacement">The text that takes each match's place: empty to delete them.</param>
    /// <returns>The number of matches replaced.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="replacement"/> is <see langword="null"/>.</exception>
    public int ReplaceAllMatches(string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (IsReadOnly || _finder is null || _findMatches.Count == 0)
        {
            return 0;
        }
        var finder = _finder;
        var source = _editor.Document.Text;
        var replacements = _findMatches
            .Select(match => new MarkdownReplacement(match.Start, match.Length, finder.Replacement(source, match, replacement)))
            .ToList();
        Replace(replacements);
        return replacements.Count;
    }

    private void Run(TextFinder finder, string text, MarkdownFindOptions options)
    {
        _finder = finder;
        _findQuery = text;
        _findOptions = options;
        Collect();
        if (_findMatches.Count == 0)
        {
            _findCurrent = -1;
        }
        else
        {
            var selectionStart = _editor.SelectionStart;
            var first = _findMatches.FindIndex(match => match.Start >= selectionStart);
            _findCurrent = first >= 0 ? first : 0;
        }
        ShowMatches(select: true);
    }

    private void Collect()
    {
        _findMatches.Clear();
        if (_finder is not null)
        {
            _findMatches.AddRange(_finder.FindAll(_editor.Document.Text));
        }
    }

    private bool Step(int direction)
    {
        if (_findMatches.Count == 0)
        {
            return false;
        }
        if (_findCurrent >= 0)
        {
            _findCurrent = (_findCurrent + direction + _findMatches.Count) % _findMatches.Count;
        }
        else if (direction > 0)
        {
            var caret = _editor.SelectionStart + _editor.SelectionLength;
            var next = _findMatches.FindIndex(match => match.Start >= caret);
            _findCurrent = next >= 0 ? next : 0;
        }
        else
        {
            var caret = _editor.SelectionStart;
            var previous = _findMatches.FindLastIndex(match => match.Start < caret);
            _findCurrent = previous >= 0 ? previous : _findMatches.Count - 1;
        }
        ShowMatches(select: true);
        return true;
    }

    /// <summary>
    /// Finds again after the text changed, by the user or from code. The match the selection is on stays current; with
    /// the selection elsewhere, no match is.
    /// </summary>
    private void KeepFind()
    {
        if (_finder is null)
        {
            return;
        }
        Collect();
        var selection = (_editor.SelectionStart, _editor.SelectionLength);
        _findCurrent = _findMatches.IndexOf(selection);
        ShowMatches(select: false);
    }

    /// <summary>
    /// Once the user moves the selection off the current match, no match is current, so the next one is found from
    /// where the selection is.
    /// </summary>
    private void OnSelectionMovedForFind()
    {
        if (_findCurrent < 0 || _selectionBatch > 0 || _editor.Document.IsInUpdate)
        {
            return;
        }
        if ((_editor.SelectionStart, _editor.SelectionLength) != _findMatches[_findCurrent])
        {
            _findCurrent = -1;
            ShowMatches(select: false);
        }
    }

    /// <summary>Highlights the matches and shows their count; with <paramref name="select"/>, selects the current one.</summary>
    private void ShowMatches(bool select)
    {
        foreach (var segment in _findHighlights.ToList())
        {
            _findHighlights.Remove(segment);
        }
        for (var i = 0; i < _findMatches.Count; i++)
        {
            var (start, length) = _findMatches[i];
            var brush = i == _findCurrent ? FindCurrentMatchBrush : FindMatchBrush;
            _findHighlights.Add(new MarkdownRangeMark(start, length, MarkdownRangeMarkStyle.Background, brush, owner: null).Segment);
        }
        _findBar?.ShowCount(CurrentFindMatch, FindMatchCount);
        if (select && _findCurrent >= 0)
        {
            var (start, length) = _findMatches[_findCurrent];
            RunBatch(() =>
            {
                _editor.Select(start, length);
                _editor.TextArea.Caret.BringCaretToView();
                BringMatchIntoView(start, length);
            });
        }
        RedrawMarks();
    }

    /// <summary>
    /// Scrolls a match into view, a third of the way down, when it is out of view or under the find bar, so it does not
    /// sit at the very edge. When scrolling cannot take it out from under the bar, as on the first lines of the text with
    /// the bar at the top, the bar moves to the other edge of the text (polhem-dev/polhem-markdown#108).
    /// </summary>
    /// <remarks>
    /// The bar stays at the edge it moved to until a match is under it there, so going through the matches does not move
    /// it back and forth. The geometry is the text view's, in its own coordinates, so it does not wait for the layout the
    /// new scroll position brings.
    /// </remarks>
    private void BringMatchIntoView(int start, int length)
    {
        if (_scrollViewer is null)
        {
            return;
        }
        var textView = _editor.TextArea.TextView;
        var document = _editor.Document;
        textView.EnsureVisualLines();
        var startPosition = new TextViewPosition(document.GetLocation(start));
        var endPosition = new TextViewPosition(document.GetLocation(start + length));
        var startTop = textView.GetVisualPosition(startPosition, VisualYPosition.LineTop);
        var endTop = textView.GetVisualPosition(endPosition, VisualYPosition.LineTop);
        var top = startTop.Y;
        var bottom = textView.GetVisualPosition(endPosition, VisualYPosition.LineBottom).Y;
        var viewport = _scrollViewer.Viewport.Height;
        var offset = _scrollViewer.Offset.Y;
        var covered = 0.0;
        if (IsFindOpen)
        {
            _findBar!.Measure(Size.Infinity);
            covered = _findBar.DesiredSize.Height + FindBarMargin;
        }
        var clearTop = _findBarAtBottom ? 0 : covered;
        var clearBottom = _findBarAtBottom ? covered : 0;
        if (top < offset + clearTop || bottom > offset + viewport - clearBottom)
        {
            var most = Math.Max(0, _scrollViewer.Extent.Height - viewport);
            offset = Math.Clamp(top - viewport / 3, 0, most);
            _scrollViewer.Offset = new Vector(_scrollViewer.Offset.X, offset);
        }
        if (covered <= 0)
        {
            return;
        }

        // The match's columns: from its start to its end on one row, the whole row when it wraps onto another.
        var scrollX = _scrollViewer.Offset.X;
        var (left, right) = Math.Abs(startTop.Y - endTop.Y) < 1
            ? (startTop.X - scrollX, endTop.X - scrollX)
            : (double.NegativeInfinity, double.PositiveInfinity);
        var barRight = textView.Bounds.Width - FindBarMargin;
        var barLeft = barRight - _findBar!.DesiredSize.Width;
        if (right < barLeft || left > barRight)
        {
            return;
        }
        bool UnderTop() => top < offset + covered;
        bool UnderBottom() => bottom > offset + viewport - covered;
        if (_findBarAtBottom ? UnderBottom() && !UnderTop() : UnderTop() && !UnderBottom())
        {
            _findBarAtBottom = !_findBarAtBottom;
            InvalidateArrange();
        }
    }
}
