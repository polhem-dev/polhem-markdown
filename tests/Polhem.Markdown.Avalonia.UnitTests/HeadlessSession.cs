using System.Reflection;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Simple;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>
/// Starts the Avalonia headless platform. Every test of <see cref="HeadlessCollection"/> runs its body on the session's
/// UI thread through <see cref="RunAsync(Func{Task})"/>, with a new application for each test.
/// </summary>
public sealed class HeadlessSession : IDisposable
{
    private readonly HeadlessUnitTestSession _session =
        HeadlessUnitTestSession.StartNew(typeof(HeadlessSession), AvaloniaTestIsolationLevel.PerTest);

    /// <summary>The application the session starts: the Simple theme on the headless platform, laying out text with Skia.</summary>
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<TestApplication>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });

    public Task RunAsync(Action action) => _session.Dispatch(action, CancellationToken.None);

    public Task RunAsync(Func<Task> action)
        => _session.Dispatch(async () =>
        {
            await action();
            return true;
        }, CancellationToken.None);

    public void Dispose() => DisposeSession(_session);

    /// <summary>
    /// Disposes <paramref name="session"/>, tolerating the startup race of Avalonia 12.0.0 that leaves the session
    /// without its dispatch task.
    /// </summary>
    /// <remarks>
    /// <c>HeadlessUnitTestSession.StartNew</c> in Avalonia before 12.1.3 starts its worker with <c>Task.Run</c> and the
    /// worker passes the task variable to the session it creates. When the worker runs before <c>Task.Run</c> returns, as it
    /// sometimes does on a busy CI runner, the session keeps a null task, and <c>Dispose</c> throws
    /// <see cref="NullReferenceException"/> on waiting for it, after it has already cancelled the worker. xUnit reports that
    /// as a collection cleanup failure although every test passed. Fixed upstream in
    /// https://github.com/AvaloniaUI/Avalonia/pull/22222; the tests stay on 12.0.0 to run against the lowest version the
    /// viewer supports, so the fix cannot be taken by upgrading.
    /// </remarks>
    internal static void DisposeSession(HeadlessUnitTestSession session)
    {
        try
        {
            session.Dispose();
        }
        catch (NullReferenceException) when (s_dispatchTaskField is not null && s_dispatchTaskField.GetValue(session) is null)
        {
            // NOTE: Only the race above lands here. The worker was cancelled before the exception and ends on its own;
            // what is skipped is waiting for it, which the session cannot do without its task.
        }
    }

    // Null if a later Avalonia renames the field, so the filter above lets the exception through instead of guessing.
    private static readonly FieldInfo? s_dispatchTaskField =
        typeof(HeadlessUnitTestSession).GetField("_dispatchTask", BindingFlags.Instance | BindingFlags.NonPublic);

    private sealed class TestApplication : Application
    {
        public override void Initialize() => Styles.Add(new SimpleTheme());
    }
}
