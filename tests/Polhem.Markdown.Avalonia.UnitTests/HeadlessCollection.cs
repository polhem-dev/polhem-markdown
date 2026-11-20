namespace Polhem.Markdown.Avalonia.UnitTests;

[CollectionDefinition(Name)]
public sealed class HeadlessCollection : ICollectionFixture<HeadlessSession>
{
    public const string Name = "Avalonia headless";
}
