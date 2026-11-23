using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class FootnoteTests(HeadlessSession session)
{
    [Fact(DisplayName = "A reference shows the note's number, links to the note, and is itself a target")]
    public Task Render_Reference_LinksToNote() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("Text[^n].\n\n[^n]: The note.");
        var paragraph = Assert.IsType<MarkdownTextBlock>(Blocks(viewer).First());

        Assert.Equal("Text1.", paragraph.Inlines!.Text);
        var link = Assert.Single(paragraph.Links);
        Assert.Equal("Text".Length, link.Start);
        Assert.Equal(1, link.Length);
        Assert.Equal("fn:1", link.AnchorId);
        Assert.Contains(paragraph.GetValue(MarkdownViewer.AnchorIdsProperty), id => id.StartsWith("fnref:", StringComparison.Ordinal));
    });

    [Fact(DisplayName = "The notes come last, numbered, each a target, and each ends with a back link to its reference")]
    public Task Render_Notes_AreNumberedTargetsWithBackLinks() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("One[^a], two[^b], again[^a].\n\n[^a]: Note A.\n\n[^b]: Note B.");
        var referenceIds = Assert.IsType<MarkdownTextBlock>(Blocks(viewer).First()).GetValue(MarkdownViewer.AnchorIdsProperty);

        var footnotes = Blocks(viewer).Last();
        var markers = Descendants<TextBlock>(footnotes).Where(text => text is not MarkdownTextBlock).Select(text => text.Text);
        Assert.Equal(["1.", "2."], markers);
        Assert.Equal(["fn:1", "fn:2"], Descendants<Control>(footnotes).SelectMany(control => control.GetValue(MarkdownViewer.AnchorIdsProperty)));

        var notes = Descendants<MarkdownTextBlock>(footnotes).ToList();
        Assert.Equal("Note A. ↩ ↩2", notes[0].Inlines!.Text);
        Assert.Equal([referenceIds[0], referenceIds[2]], notes[0].Links.Select(link => link.AnchorId));
        Assert.All(notes[0].Links, link => Assert.Equal('↩', notes[0].Inlines!.Text![link.Start]));
    });

    [Fact(DisplayName = "Following a reference brings its note into view, and the back link scrolls the reference back to the top")]
    public Task Navigate_ReferenceAndBack_Scrolls() => session.RunAsync(() =>
    {
        var filler = string.Concat(Enumerable.Repeat("A paragraph that takes up some room.\n\n", 60));
        var viewer = new MarkdownViewer { Markdown = filler + "Here[^n] it is.\n\n" + filler + "[^n]: The note." };
        var scroller = new ScrollViewer { Content = viewer };
        var window = new Window { Width = 600, Height = 300, Content = scroller };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        var paragraph = Descendants<MarkdownTextBlock>(viewer).Single(text => text.Links.Count > 0 && text.Links[0].AnchorId == "fn:1");
        var referenceId = paragraph.GetValue(MarkdownViewer.AnchorIdsProperty).Single();

        Assert.True(viewer.ScrollToAnchor("fn:1"));
        Dispatcher.UIThread.RunJobs();
        // The notes are the last block, so the view can only scroll as far as the end of the text.
        var note = Descendants<Control>(viewer).Single(control => control.GetValue(MarkdownViewer.AnchorIdsProperty).Contains("fn:1"));
        var noteTop = note.TranslatePoint(new Point(0, 0), scroller)!.Value.Y;
        Assert.InRange(noteTop, 0, scroller.Viewport.Height - note.Bounds.Height);
        Assert.Equal(scroller.Extent.Height - scroller.Viewport.Height, scroller.Offset.Y, 1);

        Assert.True(viewer.ScrollToAnchor(referenceId));
        Dispatcher.UIThread.RunJobs();
        Assert.InRange(paragraph.TranslatePoint(new Point(0, 0), scroller)!.Value.Y, -1, 1);
    });

    [Fact(DisplayName = "A reference written before its definition arrives becomes a link when the definition arrives")]
    public Task Update_DefinitionArrivesLater_RebuildsTheReference() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("Text[^n].\n\nMore.");
        Assert.Empty(((MarkdownTextBlock)Blocks(viewer).First()).Links);

        viewer.Markdown = "Text[^n].\n\nMore.\n\n[^n]: The note.";

        Assert.Equal("fn:1", Assert.Single(((MarkdownTextBlock)Blocks(viewer).First()).Links).AnchorId);
    });

    [Fact(DisplayName = "A reference added before others renumbers the later ones, though their source did not change")]
    public Task Update_EarlierReferenceArrives_Renumbers() => session.RunAsync(() =>
    {
        const string Notes = "\n\n[^a]: A.\n\n[^b]: B.";
        var (_, viewer) = Show("Intro.\n\nLater[^b]." + Notes);
        Assert.Equal("Later1.", ((MarkdownTextBlock)Blocks(viewer).ElementAt(1)).Inlines!.Text);

        viewer.Markdown = "Intro[^a].\n\nLater[^b]." + Notes;

        Assert.Equal("Later2.", ((MarkdownTextBlock)Blocks(viewer).ElementAt(1)).Inlines!.Text);
    });

    [Fact(DisplayName = "The notes use a smaller font than the text")]
    public Task Render_Notes_UseSmallerFont() => session.RunAsync(() =>
    {
        var (_, viewer) = Show("Text[^n].\n\n[^n]: The note.");
        var text = Assert.IsType<MarkdownTextBlock>(Blocks(viewer).First());
        var note = Descendants<MarkdownTextBlock>(Blocks(viewer).Last()).Single();

        Assert.True(note.FontSize < text.FontSize);
        Assert.Contains(text.Inlines!, inline => inline is Run { BaselineAlignment: global::Avalonia.Media.BaselineAlignment.Superscript });
    });
}
