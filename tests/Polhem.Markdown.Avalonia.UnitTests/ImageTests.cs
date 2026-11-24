using System.Net;
using System.Net.Sockets;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Threading;
using Polhem.Markdown.Avalonia.Images;
using SkiaSharp;
using static Polhem.Markdown.Avalonia.UnitTests.ViewerHost;

namespace Polhem.Markdown.Avalonia.UnitTests;

[Collection(HeadlessCollection.Name)]
public class ImageTests(HeadlessSession session)
{
    private const string Pen = "https://example.com/pen.png";

    /// <summary>A PNG of the given size, as an image server would send it.</summary>
    private static byte[] Png(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(SKColors.OrangeRed);
        using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static Func<Uri, CancellationToken, Task<Stream?>> Serve(byte[] bytes, List<Uri>? calls = null) => (source, _) =>
    {
        calls?.Add(source);
        return Task.FromResult<Stream?>(new MemoryStream(bytes));
    };

    /// <summary>Waits for the viewer's images to load and for the views to show them.</summary>
    private static async Task<ImageView> LoadedView(MarkdownViewer viewer)
    {
        var view = Descendants<ImageView>(viewer).Single();
        await ((Task)viewer.LoadImage(view.Source)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        Dispatcher.UIThread.RunJobs();
        return Descendants<ImageView>(viewer).Single();
    }

    /// <summary>Waits for every image the viewer shows to load or fail.</summary>
    private static async Task AllLoaded(MarkdownViewer viewer)
    {
        foreach (var view in Descendants<ImageView>(viewer).ToList())
        {
            await ((Task)viewer.LoadImage(view.Source)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing | ConfigureAwaitOptions.ContinueOnCapturedContext);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [Fact(DisplayName = "An image shows its alternative text until it has loaded, then the image at its own size")]
    public Task Render_Image_ShowsAltThenImage() => session.RunAsync(async () =>
    {
        var release = new TaskCompletionSource();
        var png = Png(40, 20);
        var (_, viewer) = Show($"A ![red pen]({Pen}) here.", images: async (_, _) =>
        {
            await release.Task;
            return new MemoryStream(png);
        });
        Assert.Equal("red pen", Assert.IsType<TextBlock>(Descendants<ImageView>(viewer).Single().Child).Text);

        release.SetResult();
        var view = await LoadedView(viewer);

        var image = Assert.IsType<Image>(view.LoadedImage);
        Assert.Equal(40, image.MaxWidth);
        Assert.Equal(20, image.MaxHeight);
    });

    [Theory(DisplayName = "An image that cannot be loaded or decoded keeps its alternative text")]
    [InlineData("nothing")]
    [InlineData("not an image")]
    [InlineData("throws")]
    [InlineData("too large")]
    public Task Render_ImageThatFails_KeepsAltText(string failure) => session.RunAsync(async () =>
    {
        Func<Uri, CancellationToken, Task<Stream?>> loader = failure switch
        {
            "nothing" => NoImages,
            "not an image" => Serve(Encoding.UTF8.GetBytes("<html>404</html>")),
            "throws" => (_, _) => Task.FromException<Stream?>(new IOException("offline")),
            _ => Serve(new byte[ImageSources.MaxBytes + 1]),
        };
        var (_, viewer) = Show($"![red pen]({Pen})", images: loader);

        var view = await LoadedView(viewer);

        Assert.Null(view.LoadedImage);
        Assert.Equal("red pen", Assert.IsType<TextBlock>(view.Child).Text);
    });

    [Fact(DisplayName = "An image is loaded once while a reply that holds it keeps arriving")]
    public Task Update_ArrivingReply_LoadsImageOnce() => session.RunAsync(async () =>
    {
        var calls = new List<Uri>();
        var (_, viewer) = Show($"![pen]({Pen}) and", images: Serve(Png(4, 4), calls));
        await LoadedView(viewer);

        viewer.Markdown = $"![pen]({Pen}) and more";
        viewer.Markdown = $"![pen]({Pen}) and more text";
        await LoadedView(viewer);

        Assert.Equal([new Uri(Pen)], calls);
        Assert.NotNull(Descendants<ImageView>(viewer).Single().LoadedImage);
    });

    [Fact(DisplayName = "Only sources with an allowed image scheme reach the loader")]
    public Task Render_RefusedSource_IsNotLoaded() => session.RunAsync(() =>
    {
        var calls = new List<Uri>();
        var (_, viewer) = Show("![a](http://example.com/a.png) ![b](file:///etc/hosts)", images: Serve(Png(4, 4), calls));

        Assert.Empty(Descendants<ImageView>(viewer));
        Assert.Empty(calls);
    });

    [Fact(DisplayName = "An SVG is drawn without fetching anything it refers to")]
    public Task Render_Svg_FetchesNothingItRefersTo() => session.RunAsync(async () =>
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var requests = 0;
        _ = Task.Run(async () =>
        {
            while (true)
            {
                using var client = await listener.AcceptTcpClientAsync();
                Interlocked.Increment(ref requests);
            }
        });
        var svg = $$"""
            <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" width="90" height="20">
              <rect width="90" height="20" fill="#4c1"/>
              <image href="http://127.0.0.1:{{port}}/a.png" width="10" height="10"/>
              <image xlink:href="http://127.0.0.1:{{port}}/b.png" width="10" height="10"/>
              <use href="http://127.0.0.1:{{port}}/c.svg#x"/>
              <rect width="5" height="5" filter="url(http://127.0.0.1:{{port}}/f.svg#f)"/>
              <style>@import url(http://127.0.0.1:{{port}}/d.css);</style>
              <script>alert(1)</script>
            </svg>
            """;
        var (_, viewer) = Show("![build passing](https://img.shields.io/badge/build-passing-green.svg)", images: Serve(Encoding.UTF8.GetBytes(svg)));

        var view = await LoadedView(viewer);
        await Task.Delay(500);

        var image = Assert.IsType<Image>(view.LoadedImage);
        Assert.Equal(90, image.MaxWidth);
        Assert.Equal(20, image.MaxHeight);
        Assert.Equal(0, requests);
    });

    [Fact(DisplayName = "An SVG that declares a DTD, as an entity expansion attack does, keeps its alternative text")]
    public Task Render_SvgWithDtd_KeepsAltText() => session.RunAsync(async () =>
    {
        const string Laughs = """
            <?xml version="1.0"?>
            <!DOCTYPE svg [<!ENTITY a "aaaaaaaaaa"><!ENTITY b "&a;&a;&a;&a;&a;&a;&a;&a;&a;&a;"><!ENTITY c "&b;&b;&b;&b;&b;&b;&b;&b;&b;&b;">]>
            <svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><text>&c;</text></svg>
            """;
        var (_, viewer) = Show("![laughs](https://img.shields.io/x.svg)", images: Serve(Encoding.UTF8.GetBytes(Laughs)));

        var view = await LoadedView(viewer);

        Assert.Null(view.LoadedImage);
    });

    [Fact(DisplayName = "A link after an image keeps its range in step with the text the layout reads")]
    public Task Render_LinkAfterImage_RangeMatchesText() => session.RunAsync(() =>
    {
        // The same paragraph with one character where the image is has to lay out to the same length.
        var (_, withImage) = Show($"x ![pen]({Pen}) [link](https://example.com/) y");
        var (_, withCharacter) = Show("x Z [link](https://example.com/) y");
        var text = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(withImage)));
        var reference = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(withCharacter)));

        Assert.Equal(reference.TextLayout.TextLines.Sum(line => line.Length), text.TextLayout.TextLines.Sum(line => line.Length));
        Assert.Equal(Assert.Single(reference.Links).Start, Assert.Single(text.Links).Start);
    });

    [Fact(DisplayName = "An image inside a link is part of the link")]
    public Task Render_ImageInLink_IsLink() => session.RunAsync(() =>
    {
        var (_, viewer) = Show($"[![pen]({Pen})](https://example.com/)");
        var text = Assert.IsType<MarkdownTextBlock>(Assert.Single(Blocks(viewer)));

        var link = Assert.Single(text.Links);
        Assert.Equal(0, link.Start);
        Assert.Equal(MarkdownRenderer.EmbeddedControlLength, link.Length);
        Assert.Contains(text.Inlines!, inline => inline is Span span && span.Inlines.OfType<InlineUIContainer>().Any());
    });

    /// <summary>A PNG whose header declares a picture of the given size and holds no pixel data.</summary>
    private static byte[] PngHeader(int width, int height)
    {
        var header = new byte[13];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header, width);
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(header.AsSpan(4), height);
        header[8] = 8;
        header[9] = 6;
        using var png = new MemoryStream();
        png.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        WriteChunk(png, "IHDR"u8, header);
        WriteChunk(png, "IEND"u8, []);
        return png.ToArray();
    }

    private static void WriteChunk(Stream png, ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        Span<byte> length = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32BigEndian(length, data.Length);
        png.Write(length);
        var typed = new byte[type.Length + data.Length];
        type.CopyTo(typed);
        data.CopyTo(typed.AsSpan(type.Length));
        png.Write(typed);
        Span<byte> crc = stackalloc byte[4];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32BigEndian(crc, Crc32(typed));
        png.Write(crc);
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
            }
        }
        return ~crc;
    }

    [Fact(DisplayName = "A PNG that declares more pixels than the limit is not decoded")]
    public Task Render_PngBomb_KeepsAltText() => session.RunAsync(async () =>
    {
        var (_, viewer) = Show($"![bomb]({Pen})", images: Serve(PngHeader(30_000, 30_000)));

        var view = await LoadedView(viewer);

        Assert.Null(view.LoadedImage);
        Assert.Equal(0, viewer.ImageBytesInUse);
    });

    [Fact(DisplayName = "An SVG nested too deeply keeps its alternative text instead of overflowing the stack")]
    public Task Render_DeepSvg_KeepsAltText() => session.RunAsync(async () =>
    {
        var svg = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\">"
            + string.Concat(Enumerable.Repeat("<g>", 5000)) + "<rect width=\"1\" height=\"1\"/>"
            + string.Concat(Enumerable.Repeat("</g>", 5000)) + "</svg>";
        var (_, viewer) = Show("![deep](https://img.shields.io/deep.svg)", images: Serve(Encoding.UTF8.GetBytes(svg)));

        var view = await LoadedView(viewer);

        Assert.Null(view.LoadedImage);
    });

    [Fact(DisplayName = "An SVG whose use elements would multiply keeps its alternative text")]
    public Task Render_SvgUseExpansion_KeepsAltText() => session.RunAsync(async () =>
    {
        var levels = new StringBuilder("<g id=\"l0\"><rect width=\"1\" height=\"1\"/></g>");
        for (var level = 1; level <= 20; level++)
        {
            levels.Append(System.Globalization.CultureInfo.InvariantCulture, $"<g id=\"l{level}\"><use href=\"#l{level - 1}\"/><use href=\"#l{level - 1}\"/></g>");
        }
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><defs>{levels}</defs><use href=\"#l20\"/></svg>";
        var (_, viewer) = Show("![bomb](https://img.shields.io/use.svg)", images: Serve(Encoding.UTF8.GetBytes(svg)));

        var view = await LoadedView(viewer);

        Assert.Null(view.LoadedImage);
    });

    [Fact(DisplayName = "An SVG may use the same simple element many times")]
    public Task Render_SvgWithSimpleUses_IsDrawn() => session.RunAsync(async () =>
    {
        var uses = string.Concat(Enumerable.Range(0, 20).Select(index => $"<use href=\"#dot\" x=\"{index}\"/>"));
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"20\" height=\"2\"><defs><rect id=\"dot\" width=\"1\" height=\"1\"/></defs>{uses}</svg>";
        var (_, viewer) = Show("![dots](https://img.shields.io/dots.svg)", images: Serve(Encoding.UTF8.GetBytes(svg)));

        var view = await LoadedView(viewer);

        Assert.NotNull(view.LoadedImage);
    });

    [Fact(DisplayName = "Decoded images stay within the viewer's budget however many a reply holds")]
    public Task Render_ManyLargeImages_StayWithinBudget() => session.RunAsync(async () =>
    {
        var svg = Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"2048\" height=\"2048\"><rect width=\"9\" height=\"9\"/></svg>");
        var markdown = string.Concat(Enumerable.Range(0, 40).Select(index => $"![{index}](https://img.shields.io/{index}.svg) "));
        var (_, viewer) = Show(markdown, images: Serve(svg));

        await AllLoaded(viewer);

        Assert.InRange(viewer.ImageBytesInUse, 1, MarkdownViewer.ImageBudgetBytes);
        Assert.Contains(Descendants<ImageView>(viewer), view => view.LoadedImage is null);
    });

    [Fact(DisplayName = "Images the text no longer shows give their bytes back")]
    public Task Update_ImagesRemoved_ReleaseBudget() => session.RunAsync(async () =>
    {
        var (_, viewer) = Show($"![pen]({Pen})", images: Serve(Png(64, 64)));
        await LoadedView(viewer);
        Assert.Equal(64 * 64 * 4, viewer.ImageBytesInUse);

        viewer.Markdown = "No images now.";

        Assert.Equal(0, viewer.ImageBytesInUse);
    });

    [Fact(DisplayName = "A reply with many images loads each once while it keeps arriving")]
    public Task Update_ManyImages_LoadEachOnce() => session.RunAsync(async () =>
    {
        var calls = new List<Uri>();
        var images = string.Concat(Enumerable.Range(0, 130).Select(index => $"![{index}](https://img.example/{index}.png) "));
        var (_, viewer) = Show(images + "and", images: Serve(Png(2, 2), calls));
        await AllLoaded(viewer);

        viewer.Markdown = images + "and more";
        viewer.Markdown = images + "and more text";

        Assert.Equal(130, calls.Distinct().Count());
        Assert.Equal(130, calls.Count);
    });

    [Theory(DisplayName = "The viewer's own loader reads only local files, never a network share")]
    [InlineData("file://attacker.example/share/x.png")]
    [InlineData("file:////attacker.example/share/x.png")]
    public async Task LoadAsync_FileWithHost_IsRefused(string source)
    {
        Assert.Null(await ImageSources.LoadAsync(new Uri(source), CancellationToken.None));
    }

    [Fact(DisplayName = "An image written by reference is rebuilt when its definition points somewhere else")]
    public Task Update_ReferenceImageRepointed_LoadsTheNewImage() => session.RunAsync(async () =>
    {
        var calls = new List<Uri>();
        var (_, viewer) = Show("![a][r]\n\n[r]: https://x.test/1.png", images: Serve(Png(8, 8), calls));
        await AllLoaded(viewer);

        viewer.Markdown = "![a][r]\n\n[r]: https://x.test/2.png";
        await AllLoaded(viewer);

        Assert.Equal(new Uri("https://x.test/2.png"), Descendants<ImageView>(viewer).Single().Source);
        Assert.Equal([new Uri("https://x.test/1.png"), new Uri("https://x.test/2.png")], calls);
        Assert.Equal(8 * 8 * 4, viewer.ImageBytesInUse);
    });

    [Fact(DisplayName = "A viewer loads only a few images at the same time")]
    public Task Render_ManyImages_LoadFewAtATime() => session.RunAsync(async () =>
    {
        var running = 0;
        var most = 0;
        var png = Png(2, 2);
        var markdown = string.Concat(Enumerable.Range(0, 20).Select(index => $"![{index}](https://img.example/{index}.png) "));
        var (_, viewer) = Show(markdown, images: async (_, _) =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref most, now);
            await Task.Delay(20, CancellationToken.None);
            Interlocked.Decrement(ref running);
            return new MemoryStream(png);
        });

        await AllLoaded(viewer);

        Assert.InRange(most, 1, MarkdownViewer.MaxConcurrentImageLoads);
    });

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current)
        {
        }
    }

    private const string Badge = "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"90\" height=\"20\"><rect width=\"90\" height=\"20\" fill=\"#4c1\"/></svg>";

    [Fact(DisplayName = "An SVG from a host that is not trusted keeps its alternative text, while a raster image from it shows")]
    public Task Render_SvgFromUntrustedHost_KeepsAltText() => session.RunAsync(async () =>
    {
        var bytes = new Dictionary<string, byte[]>
        {
            ["https://img.example/badge.svg"] = Encoding.UTF8.GetBytes(Badge),
            ["https://img.example/pen.png"] = Png(8, 8),
        };
        var (_, viewer) = Show("![svg](https://img.example/badge.svg) ![png](https://img.example/pen.png)",
            images: (source, _) => Task.FromResult<Stream?>(new MemoryStream(bytes[source.AbsoluteUri])));

        await AllLoaded(viewer);

        var views = Descendants<ImageView>(viewer).ToDictionary(view => view.Source.AbsoluteUri);
        Assert.Null(views["https://img.example/badge.svg"].LoadedImage);
        Assert.NotNull(views["https://img.example/pen.png"].LoadedImage);
    });

    [Fact(DisplayName = "A host the application adds to the trusted hosts has its SVG drawn")]
    public Task Render_SvgFromAddedHost_IsDrawn() => session.RunAsync(async () =>
    {
        var viewer = new MarkdownViewer { ImageLoader = Serve(Encoding.UTF8.GetBytes(Badge)) };
        viewer.TrustedSvgHosts.Add("badges.internal.example");
        viewer.Markdown = "![ok](https://badges.internal.example/ok.svg)";
        var window = new Window { Content = viewer };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var view = await LoadedView(viewer);

        Assert.NotNull(view.LoadedImage);
    });

    [Theory(DisplayName = "Only https SVG from a trusted host, or a GitHub workflow badge, is trusted")]
    [InlineData("https://img.shields.io/badge/a-b-green", true)]
    [InlineData("https://IMG.SHIELDS.IO/badge/a-b-green", true)]
    [InlineData("http://img.shields.io/badge/a-b-green", false)]
    [InlineData("https://shields.io.evil.example/badge", false)]
    [InlineData("https://evil.example/img.shields.io/badge", false)]
    [InlineData("https://github.com/polhem-dev/polhem-markdown/actions/workflows/build-ci.yml/badge.svg", true)]
    [InlineData("https://github.com/polhem-dev/polhem-markdown/workflows/CI/badge.svg", true)]
    [InlineData("https://github.com/polhem-dev/polhem-markdown/raw/main/logo.svg", false)]
    [InlineData("https://raw.githubusercontent.com/polhem-dev/polhem-markdown/main/badge.svg", false)]
    public void IsTrusted_Source(string source, bool trusted)
    {
        var hosts = new HashSet<string>(SvgTrust.DefaultHosts, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(trusted, SvgTrust.IsTrusted(new Uri(source), hosts));
    }

    [Fact(DisplayName = "A chain of references deep enough to overflow a normal stack does not end the process")]
    public Task Render_SvgReferenceChain_DoesNotCrash() => session.RunAsync(async () =>
    {
        var patterns = string.Concat(Enumerable.Range(0, 300).Select(index =>
            $"<pattern id=\"p{index}\" width=\"1\" height=\"1\"><rect width=\"1\" height=\"1\" fill=\"url(#p{index + 1})\"/></pattern>"));
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><defs>{patterns}</defs><rect width=\"10\" height=\"10\" fill=\"url(#p0)\"/></svg>";
        var (_, viewer) = Show("![chain](https://img.shields.io/chain.svg)", images: Serve(Encoding.UTF8.GetBytes(svg)));

        // On a thread pool thread this chain overflowed the stack and ended the test process; the load finishing at all
        // is the check. Whether Svg.Skia draws it or gives up does not matter.
        var view = await LoadedView(viewer);

        Assert.Same(view, Descendants<ImageView>(viewer).Single());
    });

    [Theory(DisplayName = "A use whose reference is padded with spaces counts as the reference it is")]
    [InlineData(" #l{0}")]
    [InlineData("#l{0} ")]
    public Task Render_SvgUseWithPaddedReference_KeepsAltText(string reference) => session.RunAsync(async () =>
    {
        var levels = new StringBuilder("<g id=\"l0\"><rect width=\"1\" height=\"1\"/></g>");
        for (var level = 1; level <= 20; level++)
        {
            var previous = string.Format(System.Globalization.CultureInfo.InvariantCulture, reference, level - 1);
            levels.Append(System.Globalization.CultureInfo.InvariantCulture, $"<g id=\"l{level}\"><use href=\"{previous}\"/><use href=\"{previous}\"/></g>");
        }
        var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"10\" height=\"10\"><defs>{levels}</defs><use href=\"#l20\"/></svg>";
        var (_, viewer) = Show("![bomb](https://img.shields.io/padded.svg)", images: Serve(Encoding.UTF8.GetBytes(svg)));

        var view = await LoadedView(viewer);

        Assert.Null(view.LoadedImage);
    });
}
