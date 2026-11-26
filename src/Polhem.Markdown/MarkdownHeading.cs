namespace Polhem.Markdown;

/// <summary>
/// A heading of a Markdown text, for a table of contents or an outline beside a viewer. Both viewers list the same
/// headings with the same ids, and scroll to one by its id.
/// </summary>
/// <param name="Level">The heading's level, from 1 for <c>#</c> to 6 for <c>######</c>.</param>
/// <param name="Text">The heading's text, without its Markdown syntax.</param>
/// <param name="Id">
/// The id GitHub gives the heading, which a link to it (<c>[text](#id)</c>) refers to. A heading that repeats an
/// earlier one's text gets <c>-1</c>, <c>-2</c> and so on, so ids are unique within a text.
/// </param>
/// <param name="SourceOffset">
/// Where the heading starts in the Markdown text, in UTF-16 code units. A host finds the section at the top of the view by
/// comparing it with the source offset there, which each viewer reports.
/// </param>
public sealed record MarkdownHeading(int Level, string Text, string Id, int SourceOffset);
