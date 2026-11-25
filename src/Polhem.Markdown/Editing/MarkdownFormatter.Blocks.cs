namespace Polhem.Markdown.Editing;

// The formats that insert a block of their own below the selection rather than format it: a table and a horizontal rule.
internal static partial class MarkdownFormatter
{
    /// <summary>The cells of an empty column: as wide as the delimiter row's <c>---</c>, so the table lines up.</summary>
    private const string EmptyCell = "     ";

    private static MarkdownEdit Table(string text, int start, int end)
    {
        string[] rows = ["|" + EmptyCell + "|" + EmptyCell + "|", "| --- | --- |", "|" + EmptyCell + "|" + EmptyCell + "|"];
        var block = InsertBlock(text, start, end, rows);

        // The caret goes in the first header cell, after the space that follows its bar.
        return block with { SelectionStart = block.SelectionStart + 2 };
    }

    private static MarkdownEdit HorizontalRule(string text, int start, int end)
    {
        const string Rule = "---";
        var block = InsertBlock(text, start, end, [Rule]);

        // The caret goes to the start of the line after the rule, so what is typed next does not join it: the blank line
        // the block ends with, the line that already follows, or a new one at the end of the text.
        var after = block.Start + block.Text.Length;
        var rest = text[(block.Start + block.Length)..];
        if (!block.Text.EndsWith(Rule, StringComparison.Ordinal))
        {
            return block with { SelectionStart = after };
        }
        if (rest.Length > 0)
        {
            return block with { SelectionStart = after + (rest[0] == '\r' ? 2 : 1) };
        }
        var lineBreak = LineBreak(text, LineAt(text, block.Start));
        return block with { Text = block.Text + lineBreak, SelectionStart = after + lineBreak.Length };
    }

    /// <summary>
    /// Inserts <paramref name="rows"/> as a block on the last line the selection touches if that line is blank, or below
    /// it otherwise, with a blank line on each side where text would otherwise touch the block. The caret is put at the
    /// start of the block.
    /// </summary>
    /// <remarks>
    /// A blank line before keeps a rule from turning the line above into a heading and a table from joining a paragraph;
    /// one after keeps the next line from becoming a row of the table.
    /// </remarks>
    private static MarkdownEdit InsertBlock(string text, int start, int end, IReadOnlyList<string> rows)
    {
        var region = Region(text, start, end);
        var line = LineAt(text, region.Start + region.Length);
        var lineBreak = LineBreak(text, line);
        var lineEnd = line.Start + line.Text.Length;
        var nextHasText = line.Break.Length > 0 && IsText(LineAt(text, lineEnd + line.Break.Length).Text);
        var block = string.Join(lineBreak, rows) + (nextHasText ? lineBreak : "");

        if (IsText(line.Text))
        {
            var before = lineBreak + lineBreak;
            return new MarkdownEdit(lineEnd, 0, before + block, lineEnd + before.Length, 0);
        }
        var previousHasText = line.Start > 0 && IsText(LineAt(text, line.Start - 1).Text);
        var prefix = previousHasText ? lineBreak : "";
        return new MarkdownEdit(line.Start, line.Text.Length, prefix + block, line.Start + prefix.Length, 0);
    }
}
