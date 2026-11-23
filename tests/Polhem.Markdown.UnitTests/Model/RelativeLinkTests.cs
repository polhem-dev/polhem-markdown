using Polhem.Markdown.Model;
using static Polhem.Markdown.UnitTests.Model.ModelTestHelpers;

namespace Polhem.Markdown.UnitTests.Model;

public class RelativeLinkTests
{
    private static readonly Uri s_base = new("https://example.com/repo/blob/main/docs/guide.md");

    private static MarkdownOptions WithBase(Uri? baseUri = null, params string[] schemes)
    {
        var options = new MarkdownOptions { BaseUri = baseUri ?? s_base };
        foreach (var scheme in schemes)
        {
            options.AllowedLinkSchemes.Add(scheme);
        }
        return options;
    }

    private static Uri? Target(string markdown, MarkdownOptions options) =>
        AllNodes(Parse(markdown, options)).OfType<LinkNode>().SingleOrDefault()?.Target;

    [Theory(DisplayName = "A relative link resolves against the base by the standard URI rules")]
    [InlineData("setup.md", "https://example.com/repo/blob/main/docs/setup.md")]
    [InlineData("./setup.md", "https://example.com/repo/blob/main/docs/setup.md")]
    [InlineData("../README.md", "https://example.com/repo/blob/main/README.md")]
    [InlineData("../../../../../x.md", "https://example.com/x.md")]
    [InlineData("/docs/setup.md", "https://example.com/docs/setup.md")]
    [InlineData("setup.md#install", "https://example.com/repo/blob/main/docs/setup.md#install")]
    [InlineData("?tab=2", "https://example.com/repo/blob/main/docs/guide.md?tab=2")]
    [InlineData("sub%20dir/a.md", "https://example.com/repo/blob/main/docs/sub%20dir/a.md")]
    public void Build_RelativeLink_ResolvesAgainstBase(string target, string expected)
    {
        Assert.Equal(new Uri(expected), Target($"[x]({target})", WithBase()));
    }

    [Fact(DisplayName = "Without a base, a relative link stays text")]
    public void Build_RelativeLinkWithoutBase_IsText()
    {
        var document = Parse("[changelog](CHANGELOG.md)");

        Assert.DoesNotContain(AllNodes(document), node => node is LinkNode);
        Assert.Equal("changelog", Text(Single<ParagraphNode>(document).Inlines));
    }

    [Fact(DisplayName = "A link to a heading of the same text stays an in-document link with a base set")]
    public void Build_AnchorLinkWithBase_StaysAnchor()
    {
        var document = Parse("[go](#setup)\n\n## Setup", WithBase());

        Assert.Single(AllNodes(document).OfType<AnchorLinkNode>());
        Assert.DoesNotContain(AllNodes(document), node => node is LinkNode);
    }

    [Theory(DisplayName = "A resolved link must still use an allowed scheme")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<b>x</b>")]
    [InlineData("vbscript:msgbox")]
    public void Build_DisallowedSchemeWithBase_IsText(string target)
    {
        Assert.Null(Target($"[x]({target})", WithBase()));
    }

    [Fact(DisplayName = "With a file base, relative links are clickable only when the host allows the file scheme")]
    public void Build_FileBase_NeedsFileScheme()
    {
        var fileBase = new Uri("file:///home/me/repo/README.md");

        Assert.Null(Target("[x](docs/setup.md)", WithBase(fileBase)));
        Assert.Equal(new Uri("file:///home/me/repo/docs/setup.md"), Target("[x](docs/setup.md)", WithBase(fileBase, "file")));
    }

    [Fact(DisplayName = "A rooted path is not read as a file on the local machine when the base is a web address")]
    public void Build_RootedPathWithWebBase_StaysOnBaseHost()
    {
        var target = Target("[x](/etc/passwd)", WithBase(null, "file"));

        Assert.Equal(new Uri("https://example.com/etc/passwd"), target);
    }

    [Fact(DisplayName = "Without a base, a rooted path stays text even when the host allows the file scheme")]
    public void Build_RootedPathWithoutBase_IsText()
    {
        var options = new MarkdownOptions();
        options.AllowedLinkSchemes.Add("file");

        Assert.Null(Target("[x](/etc/passwd)", options));
        Assert.Equal(new Uri("file:///etc/passwd"), Target("[x](file:///etc/passwd)", options));
    }

    [Fact(DisplayName = "A target that writes its scheme in capitals is still absolute")]
    public void Build_UppercaseScheme_IsAbsolute()
    {
        Assert.Equal(new Uri("https://elsewhere.example/a"), Target("[x](HTTPS://elsewhere.example/a)", WithBase()));
    }

    [Fact(DisplayName = "An absolute target written with leading space in angle brackets is still a link")]
    public void Build_AngleTargetWithLeadingSpace_IsAbsolute()
    {
        Assert.Equal(new Uri("https://example.com/a"), Target("[x](< https://example.com/a>)", new MarkdownOptions()));
    }

    [Fact(DisplayName = "A protocol-relative link takes the base's scheme")]
    public void Build_ProtocolRelative_TakesBaseScheme()
    {
        Assert.Equal(new Uri("https://other.example/x"), Target("[x](//other.example/x)", WithBase()));
    }

    [Fact(DisplayName = "An absolute link is unchanged by the base")]
    public void Build_AbsoluteLinkWithBase_IsUnchanged()
    {
        Assert.Equal(new Uri("https://elsewhere.example/a"), Target("[x](https://elsewhere.example/a)", WithBase()));
    }

    [Fact(DisplayName = "A relative base URI is refused")]
    public void BaseUri_Relative_Throws()
    {
        Assert.Throws<ArgumentException>(() => new MarkdownOptions { BaseUri = new Uri("docs/", UriKind.Relative) });
    }
}
