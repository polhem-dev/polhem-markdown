namespace Polhem.Markdown.Avalonia.Images;

/// <summary>
/// The bytes a viewer's decoded images may take. A load reserves its image's bytes before decoding and gives them back
/// when it fails or when the text no longer shows the image, so loads that run at the same time cannot pass the budget
/// together.
/// </summary>
internal sealed class ImageBudget(long capacity)
{
    private long _used;

    /// <summary>The bytes reserved now.</summary>
    public long Used => Interlocked.Read(ref _used);

    /// <summary>Reserves <paramref name="bytes"/>, or reserves nothing and returns <see langword="false"/> when they do not fit.</summary>
    public bool TryReserve(long bytes)
    {
        if (Interlocked.Add(ref _used, bytes) <= capacity)
        {
            return true;
        }
        Interlocked.Add(ref _used, -bytes);
        return false;
    }

    public void Release(long bytes) => Interlocked.Add(ref _used, -bytes);
}
