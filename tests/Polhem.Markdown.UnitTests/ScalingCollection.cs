namespace Polhem.Markdown.UnitTests;

/// <summary>
/// The test classes that hold <see cref="Scaling"/> checks. They run alone, never beside other test classes: a check
/// compares run times, and on a build agent with two processors another class running at the same time moved the ratio
/// of a linear case from about 4 to anywhere between 0.4 and 13.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class ScalingCollection
{
    public const string Name = "Scaling";
}
