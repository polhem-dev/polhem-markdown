using System.Reflection;

namespace Polhem.Markdown.UnitTests;

/// <summary>
/// Each package references only .NET, the other packages of this repository and its own external package. The
/// build-time gate in src/Directory.Build.targets checks the package references; these tests check the assemblies each
/// package actually references, and that every package under src is listed in <see cref="Packages"/>.
/// </summary>
public class DependencyGateTests
{
    private static readonly string[] s_dotNet = ["System", "netstandard", "Microsoft.Extensions"];

    [Fact(DisplayName = "Every package under src is listed, so a new package cannot go unchecked")]
    public void PackageList_ProjectsUnderSrc_AllListed()
    {
        var projects = Directory.GetFiles(RepositoryPaths.SourceDirectory(), "*.csproj", SearchOption.AllDirectories)
            .Select(Path.GetFileNameWithoutExtension)
            .Order(StringComparer.Ordinal);

        Assert.Equal(Packages.AllowedReferences.Keys.Order(StringComparer.Ordinal), projects);
    }

    [Theory(DisplayName = "Each package's assembly references only .NET and the assemblies allowed for it")]
    [MemberData(nameof(Packages.Names), MemberType = typeof(Packages))]
    public void ReferencedAssemblies_EachPackage_AreAllowed(string package)
    {
        var allowed = Packages.AllowedReferences[package].Concat(s_dotNet).ToArray();

        var unexpected = Assembly.Load(package).GetReferencedAssemblies()
            .Select(reference => reference.Name!)
            .Where(name => !allowed.Any(prefix => name == prefix || name.StartsWith(prefix + ".", StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(unexpected);
    }
}
