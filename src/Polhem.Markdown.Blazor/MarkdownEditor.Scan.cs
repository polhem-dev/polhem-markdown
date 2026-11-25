using System.Diagnostics;
using System.Runtime.InteropServices;
using Polhem.Markdown.Editing;

namespace Polhem.Markdown.Blazor;

// The coloring: the text is scanned on a background thread a moment after typing stops, and only the spans that changed
// are sent to the module, as a patch on those it has.
public partial class MarkdownEditor
{
    /// <summary>How long after the last edit a text is scanned again, before the time of the last scan is added.</summary>
    private const int ScanDelayMilliseconds = 150;

    /// <summary>
    /// The most the wait before a scan grows. Under WebAssembly the scan runs on the page's only thread, so a large text,
    /// whose scan holds the page longer, waits longer for typing to stop.
    /// </summary>
    private const int MaxScanDelayMilliseconds = 1000;

    /// <summary>The spans the module has, and the length of the text they are for, to send the next scan as a patch.</summary>
    private int[] _sentSpans = [];
    private int _sentLength;

    private double _lastScanMilliseconds;
    private CancellationTokenSource? _scan;
    private Task _scanTask = Task.CompletedTask;

    private void ScheduleScan(bool immediately)
    {
        _scan?.Cancel();
        _scan?.Dispose();
        _scan = new CancellationTokenSource();
        var delay = immediately
            ? 0
            : Math.Min(MaxScanDelayMilliseconds, ScanDelayMilliseconds + (int)(2 * _lastScanMilliseconds));
        _scanTask = ScanAsync(delay, _scan.Token);
    }

    /// <summary>
    /// Scans the text after <paramref name="delay"/> milliseconds and sends the module the spans that changed, unless
    /// another edit came meanwhile; the scan that edit scheduled sends them instead.
    /// </summary>
    private async Task ScanAsync(int delay, CancellationToken token)
    {
        try
        {
            await Task.Delay(delay, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        var text = _text;
        var version = _version;
        var watch = Stopwatch.StartNew();
        var spans = SyntaxSpanPatch.Pack(await Task.Run(() => MarkdownSyntaxScanner.Scan(text), CancellationToken.None));
        _lastScanMilliseconds = watch.Elapsed.TotalMilliseconds;
        if (token.IsCancellationRequested || _module is null || _disposed)
        {
            return;
        }

        var patch = SyntaxSpanPatch.Diff(_sentSpans, _sentLength, spans, text.Length);
        _sentSpans = spans;
        _sentLength = text.Length;
        await CallModuleAsync("applySpans", _root, version, MemoryMarshal.AsBytes(patch.AsSpan()).ToArray());
    }
}
