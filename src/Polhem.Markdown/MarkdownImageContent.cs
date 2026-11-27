namespace Polhem.Markdown;

/// <summary>
/// The bytes of an image and their media type, which <see cref="MarkdownHtml"/> writes into the page as a <c>data:</c>
/// URI in place of the image's link (see <see cref="MarkdownHtmlOptions.ImageContent"/>).
/// </summary>
public sealed class MarkdownImageContent
{
    /// <summary>Initializes a new instance of the <see cref="MarkdownImageContent"/> class.</summary>
    /// <param name="mediaType">The media type of the image, such as <c>image/png</c>.</param>
    /// <param name="data">The bytes of the image.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="mediaType"/> is not an <c>image/</c> media type made of the letters, digits and punctuation a media
    /// type allows, with no parameters.
    /// </exception>
    public MarkdownImageContent(string mediaType, ReadOnlyMemory<byte> data)
    {
        ArgumentNullException.ThrowIfNull(mediaType);
        if (!IsImageMediaType(mediaType))
        {
            throw new ArgumentException("The media type must be an image type such as image/png, with no parameters.", nameof(mediaType));
        }
        MediaType = mediaType;
        Data = data;
    }

    /// <summary>Gets the media type of the image, such as <c>image/png</c>.</summary>
    public string MediaType { get; }

    /// <summary>Gets the bytes of the image.</summary>
    public ReadOnlyMemory<byte> Data { get; }

    /// <summary>The image as a <c>data:</c> URI.</summary>
    internal string ToDataUri() => "data:" + MediaType + ";base64," + Convert.ToBase64String(Data.Span);

    // NOTE: the type goes into the URI as written, so only the characters RFC 6838 allows in a type name are accepted;
    // anything else, a comma or a quote included, could end the media type early or the attribute it is written in.
    private static bool IsImageMediaType(string mediaType)
    {
        const string Prefix = "image/";
        if (!mediaType.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) || mediaType.Length == Prefix.Length)
        {
            return false;
        }
        foreach (var character in mediaType.AsSpan(Prefix.Length))
        {
            if (!char.IsAsciiLetterOrDigit(character) && character is not ('!' or '#' or '$' or '&' or '-' or '^' or '_' or '.' or '+'))
            {
                return false;
            }
        }
        return true;
    }
}
