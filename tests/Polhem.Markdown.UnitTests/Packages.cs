namespace Polhem.Markdown.UnitTests;

/// <summary>
/// The packages under src, with the assemblies each one may reference besides .NET.
/// <see cref="DependencyGateTests.PackageList_ProjectsUnderSrc_AllListed"/> fails when a package under src is missing
/// here, so every gate that reads this list covers a new package too.
/// </summary>
internal static class Packages
{
    public static readonly IReadOnlyDictionary<string, string[]> AllowedReferences = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["Polhem.Markdown"] = ["Markdig"],
        ["Polhem.Markdown.Avalonia"] = ["Polhem.Markdown", "Avalonia", "AvaloniaEdit", "Svg", "SkiaSharp"],
        ["Polhem.Markdown.Blazor"] = ["Polhem.Markdown", "Microsoft.AspNetCore.Components", "Microsoft.JSInterop"],
    };

    public static TheoryData<string> Names => [.. AllowedReferences.Keys];
}
