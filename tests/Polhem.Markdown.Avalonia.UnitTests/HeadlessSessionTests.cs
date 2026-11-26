using System.Reflection;
using Avalonia;
using Avalonia.Headless;

namespace Polhem.Markdown.Avalonia.UnitTests;

/// <summary>Disposing the headless session when Avalonia 12.0.0's startup race left it without its dispatch task.</summary>
public class HeadlessSessionTests
{
    [Fact(DisplayName = "A session the startup race left without its dispatch task disposes without throwing")]
    public void DisposeSession_ToleratesMissingDispatchTask()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(Application), AvaloniaTestIsolationLevel.PerTest);
        var field = typeof(HeadlessUnitTestSession).GetField("_dispatchTask", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        // The race: the worker created the session before the task variable it captured was assigned.
        field.SetValue(session, null);

        // Avalonia's own Dispose still throws here; once it stops, the workaround in HeadlessSession can go.
        Assert.Throws<NullReferenceException>(session.Dispose);
        HeadlessSession.DisposeSession(session);
    }
}
