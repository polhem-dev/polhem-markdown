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

    public void Dispose() => _session.Dispose();

    private sealed class TestApplication : Application
    {
        public override void Initialize() => Styles.Add(new SimpleTheme());
    }
}
