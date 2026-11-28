using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class MarkdownEditorPreviewTests(HeadlessSession session)
{
    private const string Original = "one two three";

    private static MarkdownEditor Show(string markdown = Original)
    {
        var editor = new MarkdownEditor { Markdown = markdown };
        var window = new Window { Width = 600, Height = 400, Content = editor };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    private static void Type(MarkdownEditor editor, int offset, string text)
    {
        editor.CaretOffset = offset;
        var area = editor.TextEditor.TextArea;
        foreach (var character in text)
        {
            area.RaiseEvent(new TextInputEventArgs { RoutedEvent = InputElement.TextInputEvent, Text = character.ToString() });
        }
    }

    [Fact(DisplayName = "PreviewReplace inserts the proposed text after the range and leaves the range in place")]
    public Task PreviewReplace_InsertsAfterRange() => session.RunAsync(() =>
    {
        var editor = Show();

        var preview = editor.PreviewReplace(4, 3, "2");

        Assert.Equal("one two2 three", editor.Markdown);
        Assert.True(preview.IsPending);
        Assert.Equal(4, preview.Start);
        Assert.Equal(3, preview.Length);
        Assert.Equal(7, preview.ProposedStart);
        Assert.Equal("2", preview.Text);
    });

    [Fact(DisplayName = "Accepting leaves one step to undo, which turns the new text back into the original")]
    public Task Accept_OneUndoRestoresOriginal() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "2");

        Assert.True(preview.Accept());

        Assert.Equal("one 2 three", editor.Markdown);
        Assert.False(preview.IsPending);
        Assert.True(editor.Undo());
        Assert.Equal(Original, editor.Markdown);
        Assert.False(editor.Undo());
        Assert.True(editor.Redo());
        Assert.Equal("one 2 three", editor.Markdown);
        Assert.False(editor.Redo());
    });

    [Fact(DisplayName = "Rejecting removes the proposed text and leaves nothing to undo or redo")]
    public Task Reject_LeavesNoSteps() => session.RunAsync(() =>
    {
        var editor = Show();
        editor.Replace(0, 3, "ONE");
        var preview = editor.PreviewReplace(4, 3, "2");

        Assert.True(preview.Reject());

        Assert.Equal("ONE two three", editor.Markdown);
        Assert.False(editor.Redo());
        Assert.True(editor.Undo());
        Assert.Equal(Original, editor.Markdown);
        Assert.False(editor.Undo());
    });

    [Fact(DisplayName = "Edits before and after the proposed text keep their own steps when the preview is accepted")]
    public Task Accept_KeepsEditsElsewhere() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "2");

        Type(editor, 0, "A");
        editor.Replace(editor.Markdown!.Length, 0, "!");
        Assert.Equal("Aone two2 three!", editor.Markdown);
        Assert.True(preview.Accept());

        Assert.Equal("Aone 2 three!", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal("Aone two three!", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal("Aone two three", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal(Original, editor.Markdown);
        Assert.False(editor.Undo());
    });

    [Fact(DisplayName = "Edits before and after the proposed text keep their own steps when the preview is rejected")]
    public Task Reject_KeepsEditsElsewhere() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "2");

        editor.Replace(editor.Markdown!.Length, 0, "!");
        Type(editor, 0, "A");
        Assert.True(preview.Reject());

        Assert.Equal("Aone two three!", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal("one two three!", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal(Original, editor.Markdown);
        Assert.False(editor.Undo());
        Assert.True(editor.Redo());
        Assert.True(editor.Redo());
        Assert.Equal("Aone two three!", editor.Markdown);
    });

    [Fact(DisplayName = "An edit undone while the preview is pending stays undone, and can no longer be redone")]
    public Task Resolve_AfterUndoingAnEdit() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "2");
        editor.Replace(0, 0, "A");
        editor.Replace(editor.Markdown!.Length, 0, "!");
        Assert.True(editor.Undo());

        Assert.True(preview.Accept());

        Assert.Equal("Aone 2 three", editor.Markdown);
        Assert.False(editor.Redo());
        Assert.True(editor.Undo());
        Assert.True(editor.Undo());
        Assert.Equal(Original, editor.Markdown);
    });

    [Fact(DisplayName = "An edit inside the original range is its own step, and accepting replaces the range as it is then")]
    public Task Accept_AfterEditingTheOriginalRange() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "2");

        editor.Replace(5, 1, "W");
        Assert.Equal("one tWo2 three", editor.Markdown);
        Assert.Equal("tWo", editor.Markdown!.Substring(preview.Start, preview.Length));
        Assert.True(preview.Accept());

        Assert.Equal("one 2 three", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal("one tWo three", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.Equal(Original, editor.Markdown);
    });

    [Fact(DisplayName = "The user cannot type into the proposed text or delete it, but can type where it starts and ends")]
    public Task ProposedText_IsReadOnlyToTheUser() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "234");

        Type(editor, 8, "x");
        Assert.Equal("one two234 three", editor.Markdown);

        editor.Focus();
        editor.Select(6, 4);
        ((Window)TopLevel.GetTopLevel(editor)!).KeyPressQwerty(PhysicalKey.Delete, RawInputModifiers.None);
        Assert.Equal("one tw234 three", editor.Markdown);

        Type(editor, preview.ProposedStart, "<");
        Type(editor, preview.ProposedStart + preview.Text.Length, ">");
        Assert.Equal("one tw<234> three", editor.Markdown);
        Assert.True(preview.IsPending);

        Assert.True(preview.Accept());
        Assert.Equal("one 234> three", editor.Markdown);
    });

    [Fact(DisplayName = "Undoing the step that showed the preview takes it away, and redoing it brings it back")]
    public Task UndoAndRedo_ThePreview() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "2");
        var changes = 0;
        preview.PendingChanged += (_, _) => changes++;

        Assert.True(editor.Undo());
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(Original, editor.Markdown);
        Assert.False(preview.IsPending);
        Assert.False(preview.Accept());
        Assert.Equal(1, changes);

        Assert.True(editor.Redo());
        Dispatcher.UIThread.RunJobs();
        Assert.True(preview.IsPending);
        Assert.Equal(2, changes);

        Assert.True(preview.Reject());
        Assert.Equal(3, changes);
        Assert.Equal(Original, editor.Markdown);
        Assert.False(editor.Undo());
    });

    [Fact(DisplayName = "GetMarkdownWithoutPreview leaves out the proposed text of a pending preview only")]
    public Task GetMarkdownWithoutPreview_LeavesOutProposedText() => session.RunAsync(() =>
    {
        var editor = Show();
        Assert.Equal(Original, editor.GetMarkdownWithoutPreview());

        var preview = editor.PreviewReplace(4, 3, "2");
        editor.Replace(0, 0, "A");

        Assert.Equal("Aone two three", editor.GetMarkdownWithoutPreview());
        Assert.True(preview.Accept());
        Assert.Equal("Aone 2 three", editor.GetMarkdownWithoutPreview());
    });

    [Fact(DisplayName = "A deletion and an insertion can be previewed too")]
    public Task Preview_DeletionAndInsertion() => session.RunAsync(() =>
    {
        var editor = Show();

        var deletion = editor.PreviewReplace(3, 4, "");
        Assert.Equal(Original, editor.Markdown);
        Assert.True(deletion.Accept());
        Assert.Equal("one three", editor.Markdown);

        var insertion = editor.PreviewReplace(3, 0, " and");
        Assert.Equal("one and three", editor.Markdown);
        Assert.True(insertion.Reject());
        Assert.Equal("one three", editor.Markdown);

        Assert.True(editor.Undo());
        Assert.Equal(Original, editor.Markdown);
        Assert.False(editor.Undo());
    });

    [Fact(DisplayName = "A change from code to the proposed text ends the preview and keeps the text as an ordinary edit")]
    public Task CodeChangeToProposedText_EndsPreview() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "234");
        var changes = 0;
        preview.PendingChanged += (_, _) => changes++;

        editor.Replace(8, 1, "x");
        Dispatcher.UIThread.RunJobs();

        Assert.False(preview.IsPending);
        Assert.Equal(1, changes);
        Assert.False(preview.Reject());
        Assert.Equal("one two2x4 three", editor.Markdown);
        Assert.True(editor.Undo());
        Assert.True(editor.Undo());
        Assert.Equal(Original, editor.Markdown);
        Assert.False(preview.IsPending);
    });

    [Fact(DisplayName = "Setting Markdown ends the preview")]
    public Task SettingMarkdown_EndsPreview() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "2");

        editor.Markdown = "other";

        Assert.False(preview.IsPending);
        Assert.False(preview.Accept());
        Assert.Equal("other", editor.Markdown);
        Assert.Equal("other", editor.GetMarkdownWithoutPreview());
    });

    [Fact(DisplayName = "One preview is pending at a time, and an undone one cannot come back once another starts")]
    public Task OnePreviewAtATime() => session.RunAsync(() =>
    {
        var editor = Show();
        var first = editor.PreviewReplace(4, 3, "2");

        Assert.Throws<InvalidOperationException>(() => editor.PreviewReplace(0, 3, "1"));
        Assert.True(editor.Undo());

        var second = editor.PreviewReplace(0, 3, "1");
        Assert.False(editor.Redo());
        Assert.False(first.IsPending);
        Assert.True(second.IsPending);
    });

    [Fact(DisplayName = "PreviewReplace rejects a range outside the text, null text and an empty change")]
    public Task PreviewReplace_InvalidArguments_Throw() => session.RunAsync(() =>
    {
        var editor = Show();

        Assert.Throws<ArgumentOutOfRangeException>(() => editor.PreviewReplace(-1, 1, "x"));
        Assert.Throws<ArgumentOutOfRangeException>(() => editor.PreviewReplace(10, 10, "x"));
        Assert.Throws<ArgumentNullException>(() => editor.PreviewReplace(0, 1, null!));
        Assert.Throws<ArgumentException>(() => editor.PreviewReplace(2, 0, ""));
        Assert.Equal(Original, editor.Markdown);
    });

    [Fact(DisplayName = "The preview is drawn with the preview brushes, and its marks go when it is resolved")]
    public Task Preview_Marks() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "2");

        Assert.Equal(
            [(4, 3, MarkdownRangeMarkStyle.Background), (4, 3, MarkdownRangeMarkStyle.Strikethrough), (7, 1, MarkdownRangeMarkStyle.Background)],
            editor.PreviewMarks.Select(mark => (mark.Start, mark.Length, mark.Style)));
        Assert.Empty(editor.Marks);

        preview.Accept();
        Assert.Empty(editor.PreviewMarks);
    });

    [Fact(DisplayName = "Accepting reports Markdown once, with the final text, and keeps the caret after the new text")]
    public Task Accept_ReportsOnceAndPlacesCaret() => session.RunAsync(() =>
    {
        var editor = Show();
        var preview = editor.PreviewReplace(4, 3, "2");
        editor.Replace(0, 0, "A");
        editor.CaretOffset = preview.ProposedStart;
        var reported = new List<string?>();
        editor.PropertyChanged += (_, e) =>
        {
            if (e.Property == MarkdownEditor.MarkdownProperty)
            {
                reported.Add(editor.Markdown);
            }
        };

        preview.Accept();

        Assert.Equal(["Aone 2 three"], reported);
        Assert.Equal(6, editor.CaretOffset);
    });
}
