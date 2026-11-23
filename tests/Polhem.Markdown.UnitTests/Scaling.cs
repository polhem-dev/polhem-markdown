using System.Diagnostics;

namespace Polhem.Markdown.UnitTests;

/// <summary>
/// Checks that work grows in step with its input. Text from people and AI models can be built to hit a slow path, and it
/// is parsed again on every chunk while it arrives, so a cost that grows with the square of the input is a fault.
/// </summary>
/// <remarks>
/// NOTE: the check compares two sizes rather than a time limit. A limit fails on a slow machine, or under the coverage
/// collector of the sonarcloud job, without any fault; four times the input takes about four times as long when the work
/// is linear and about sixteen times when it is quadratic, wherever it runs. To keep noise out of that ratio:
/// <list type="bullet">
/// <item>the smaller input is doubled until one run of it takes a few milliseconds, so no run is too short to time;</item>
/// <item>each measurement times the smaller and the larger input back to back, so both meet the same load on a busy
/// machine;</item>
/// <item>the ratio is the median of several such measurements, so one interrupted run does not decide it.</item>
/// </list>
/// </remarks>
internal static class Scaling
{
    /// <summary>
    /// The trait of the tests that use this check. The sonarcloud job leaves them out: its coverage collector changes
    /// how long code takes, and the build job runs them without it.
    /// </summary>
    public const string Category = "Scaling";

    private const int Factor = 4;
    private const double Limit = 10;
    private const int Pairs = 5;
    private static readonly TimeSpan s_shortest = TimeSpan.FromMilliseconds(3);

    /// <summary>
    /// Asserts that running <paramref name="run"/> on <c>input(size * 4)</c> takes less than ten times as long as on
    /// <c>input(size)</c>, where <c>size</c> starts at <paramref name="start"/> and doubles, up to
    /// <paramref name="largest"/>, until one run takes a few milliseconds.
    /// </summary>
    public static void AssertLinear(Func<int, string> input, Action<string> run, int start, int largest, string what)
    {
        var size = start;
        var small = input(size);
        run(small);
        while (Time(run, small) < s_shortest && size * 2 <= largest)
        {
            size *= 2;
            small = input(size);
        }
        var large = input(size * Factor);
        run(large);

        var ratios = new double[Pairs];
        for (var pair = 0; pair < Pairs; pair++)
        {
            var smallTime = Time(run, small);
            var largeTime = Time(run, large);
            ratios[pair] = largeTime.TotalMilliseconds / Math.Max(smallTime.TotalMilliseconds, 0.001);
        }
        Array.Sort(ratios);
        var median = ratios[Pairs / 2];
        Assert.True(median < Limit, $"{what}: {Factor} times the input (size {size}) took {median:0.0} times as long ({string.Join(", ", ratios.Select(ratio => ratio.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)))}).");
    }

    private static TimeSpan Time(Action<string> run, string input)
    {
        var watch = Stopwatch.StartNew();
        run(input);
        watch.Stop();
        return watch.Elapsed;
    }
}
