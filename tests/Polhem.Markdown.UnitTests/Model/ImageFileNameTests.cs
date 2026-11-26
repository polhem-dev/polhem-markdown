namespace Polhem.Markdown.UnitTests.Model;

/// <summary>The name an image is saved under (polhem-dev/polhem-markdown#88).</summary>
public class ImageFileNameTests
{
    private static readonly byte[] s_png = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A];

    [Theory(DisplayName = "The name is the last segment of the address, unescaped, with characters files cannot have replaced")]
    [InlineData("https://example.org/docs/chart.png", "chart.png")]
    [InlineData("https://example.org/docs/my%20chart.png", "my chart.png")]
    [InlineData("https://example.org/a%3Ab%3F.png", "a_b_.png")]
    [InlineData("https://example.org/chart.png?size=2", "chart.png")]
    [InlineData("file:///Users/me/shot.jpg", "shot.jpg")]
    public void Suggest_TakesLastSegment(string source, string expected)
    {
        Assert.Equal(expected, ImageFileName.Suggest(new Uri(source), s_png));
    }

    [Theory(DisplayName = "An address with no name or no image extension gets one for the format its bytes start as")]
    [InlineData("https://img.shields.io/badge/build-passing", "<svg xmlns=\"http://www.w3.org/2000/svg\">", "build-passing.svg")]
    [InlineData("https://example.org/", "GIF89a", "image.gif")]
    [InlineData("https://example.org/render", "RIFF0000WEBPVP8", "render.webp")]
    [InlineData("https://example.org/plain", "not an image", "plain")]
    public void Suggest_AddsExtension(string source, string start, string expected)
    {
        Assert.Equal(expected, ImageFileName.Suggest(new Uri(source), System.Text.Encoding.UTF8.GetBytes(start)));
    }

    [Fact(DisplayName = "PNG, JPEG and SVG with an XML declaration are told by their bytes")]
    public void ExtensionOf_TellsFormats()
    {
        Assert.Equal(".png", ImageFileName.ExtensionOf(s_png));
        Assert.Equal(".jpg", ImageFileName.ExtensionOf([0xFF, 0xD8, 0xFF, 0xE0]));
        Assert.Equal(".svg", ImageFileName.ExtensionOf("<?xml version=\"1.0\"?>\n<svg/>"u8));
        Assert.Equal(".bmp", ImageFileName.ExtensionOf("BM...."u8));
        Assert.Null(ImageFileName.ExtensionOf([]));
    }

    [Fact(DisplayName = "A very long name is cut short")]
    public void Suggest_CutsLongName()
    {
        var name = ImageFileName.Suggest(new Uri("https://example.org/" + new string('a', 300) + ".png"), s_png);

        Assert.True(name.Length <= 124);
    }
}
