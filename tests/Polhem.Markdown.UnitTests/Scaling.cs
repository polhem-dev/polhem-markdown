using System.Diagnostics;

namespace Polhem.Markdown.UnitTests;

/// <summary>
/// Checks that work grows in step with its input. Text from people and AI models can be built to hit a slow path, and it
/// is parsed again on every chunk while it arrives, so a cost that grows with the square of the input is a fault.
/// </summary>
/// <remarks>
/// NOTE: the check compares two sizes rather than a time limit. A limit fails on a slow machine, or under the coverage
/// collector of the sonarcloud job, without any fault; four times the input takes about four times as long when the work
/// is linear and about sixteen times when it is quadratic, wherever it runs.
/// </remarks>
internal static class Scaling
{
    private const int Factor = 4;
    private const double Limit = 8;

    /// <summary>
    /// Asserts that running <paramref name="run"/> on <c>input(size * 4)</c> takes less than eight times as long as on
    /// <c>input(size)</c>.
    /// </summary>
    public static void AssertLinear(Func<int, string> input, Action<string> run, int size, string what)
    {
        var small = input(size);
        var large = input(size * Factor);
        run(input(Math.Max(1, size / Factor)));

        var smallTime = Fastest(() => run(small));
        var largeTime = Fastest(() => run(large));

        // A floor keeps a run too short to time from turning noise into a large ratio.
        var ratio = largeTime.TotalMilliseconds / Math.Max(smallTime.TotalMilliseconds, 1);
        Assert.True(ratio < Limit, $"{what}: {Factor} times the input took {ratio:0.0} times as long ({smallTime.TotalMilliseconds:0.0} ms, then {largeTime.TotalMilliseconds:0.0} ms).");
    }

    /// <summary>The fastest of three runs, so a pause for garbage collection or another process does not count.</summary>
    private static TimeSpan Fastest(Action action)
    {
        var fastest = TimeSpan.MaxValue;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var watch = Stopwatch.StartNew();
            action();
            watch.Stop();
            if (watch.Elapsed < fastest)
            {
                fastest = watch.Elapsed;
            }
        }
        return fastest;
    }
}
