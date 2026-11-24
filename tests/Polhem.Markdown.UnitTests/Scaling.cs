using System.Diagnostics;
using System.Runtime;

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
/// <item>the smaller input is doubled until the fastest of a few runs of it takes a few milliseconds, so no run is too
/// short to time, or until the larger input would allocate more than the runtime can reserve (see the next item);</item>
/// <item>both inputs run several times before any is timed, so the code being timed has been compiled to its final
/// tier rather than measured partly before and partly after the JIT recompiles it;</item>
/// <item>no garbage collection happens during a timed run, when the runtime can reserve the memory the run allocates.
/// A collection during a run walks the objects that run has built so far, so the larger input pays for more and larger
/// collections: that alone took the ratio of linear cases from 4 to between 8 and 15, depending on where the
/// collector's budget happened to fall;</item>
/// <item>each size is timed several times, alternating, and its fastest run is kept: interruptions only ever add time,
/// so the fastest run is the closest to the cost of the work itself.</item>
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
    private const int Rounds = 5;
    private const int WarmUps = 3;
    // NOTE: the runtime refuses to reserve a few hundred megabytes for a region without collections, and some inputs
    // allocate over a hundred bytes per character, so the size stops growing before the larger input would need more than this.
    private const long MaxReserved = 64 << 20;
    private static readonly TimeSpan s_shortest = TimeSpan.FromMilliseconds(3);

    /// <summary>
    /// Asserts that the fastest run of <paramref name="run"/> on <c>input(size * 4)</c> takes less than ten times as long
    /// as the fastest on <c>input(size)</c>, where <c>size</c> starts at <paramref name="start"/> and doubles, up to
    /// <paramref name="largest"/>, until its fastest run takes a few milliseconds or the larger input would allocate too much
    /// to time without collections.
    /// </summary>
    public static void AssertLinear(Func<int, string> input, Action<string> run, int start, int largest, string what)
    {
        var size = start;
        var small = input(size);
        run(small);
        while (size * 2 <= largest && Fastest(run, small) < s_shortest && Allocated(run, small) * 2 * Factor <= MaxReserved)
        {
            size *= 2;
            small = input(size);
        }
        var large = input(size * Factor);
        for (var warmUp = 0; warmUp < WarmUps; warmUp++)
        {
            run(small);
            run(large);
        }
        var smallMemory = Allocated(run, small);
        var largeMemory = Allocated(run, large);

        var smallFastest = TimeSpan.MaxValue;
        var largeFastest = TimeSpan.MaxValue;
        var ratios = new double[Rounds];
        for (var round = 0; round < Rounds; round++)
        {
            var smallTime = TimeWithoutCollection(run, small, smallMemory);
            var largeTime = TimeWithoutCollection(run, large, largeMemory);
            smallFastest = smallTime < smallFastest ? smallTime : smallFastest;
            largeFastest = largeTime < largeFastest ? largeTime : largeFastest;
            ratios[round] = largeTime.TotalMilliseconds / Math.Max(smallTime.TotalMilliseconds, 0.001);
        }
        var ratio = largeFastest.TotalMilliseconds / Math.Max(smallFastest.TotalMilliseconds, 0.001);
        Assert.True(ratio < Limit, $"{what}: {Factor} times the input (size {size}) took {Format(ratio)} times as long, at best {Milliseconds(smallFastest)} ms against {Milliseconds(largeFastest)} ms (ratios of each round: {string.Join(", ", ratios.Select(Format))}).");
    }

    private static string Milliseconds(TimeSpan time) => Format(time.TotalMilliseconds);

    private static string Format(double value) => value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The fastest of a few runs, so that one slow run, compiling or interrupted, does not stop the doubling early.</summary>
    private static TimeSpan Fastest(Action<string> run, string input)
    {
        var fastest = TimeSpan.MaxValue;
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var time = Time(run, input);
            fastest = time < fastest ? time : fastest;
        }
        return fastest;
    }

    private static long Allocated(Action<string> run, string input)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        run(input);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    /// <summary>
    /// Times one run, keeping the garbage collector out of it when <paramref name="memory"/>, what the run allocates, can
    /// be reserved beforehand; otherwise the run starts on a collected heap.
    /// </summary>
    private static TimeSpan TimeWithoutCollection(Action<string> run, string input, long memory)
    {
        // NOTE: starting the region collects the whole heap first, so either way the run starts on a collected heap.
        if (!TryStartNoCollection(memory + memory / 2 + (1 << 20)))
        {
            GC.Collect();
            return Time(run, input);
        }
        try
        {
            return Time(run, input);
        }
        finally
        {
            // The region ends by itself, with a collection, when the run allocates more than was reserved.
            if (GCSettings.LatencyMode == GCLatencyMode.NoGCRegion)
            {
                GC.EndNoGCRegion();
            }
        }
    }

    private static TimeSpan Time(Action<string> run, string input)
    {
        var watch = Stopwatch.StartNew();
        run(input);
        watch.Stop();
        return watch.Elapsed;
    }

    private static bool TryStartNoCollection(long memory)
    {
        try
        {
            return GC.TryStartNoGCRegion(memory);
        }
        catch (ArgumentOutOfRangeException)
        {
            // More than the runtime can reserve at once: the run is timed with collections.
            return false;
        }
    }
}
