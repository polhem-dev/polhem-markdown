using Microsoft.JSInterop;

namespace Polhem.Markdown.Blazor;

// Finding text in the viewer from code (polhem-dev/polhem-markdown#85). The browser's own find already works on the
// viewer's text; this is for a host that searches from its own controls, as the Avalonia viewer's API lets it.
public partial class MarkdownViewer
{
    /// <summary>Gets the number of matches of the last find, or 0.</summary>
    public int FindMatchCount { get; private set; }

    /// <summary>Gets the position of the current match among the matches, from 1, or 0 when there is none.</summary>
    public int CurrentFindMatch { get; private set; }

    /// <summary>
    /// Finds <paramref name="text"/> in the viewer, highlights every match, and goes to the first one, scrolling it to the
    /// middle of the view and opening a closed section around it. Case is ignored unless <paramref name="matchCase"/>. A
    /// match never spans two blocks, but runs across emphasis and links, and finds an image by its alternative text and a
    /// formula by its LaTeX, as the Avalonia viewer's <c>Find</c> does. Returns the number of matches.
    /// </summary>
    /// <remarks>
    /// The highlights use the browser's CSS Custom Highlight API, named <c>pmd-find</c> and <c>pmd-find-current</c>, so a
    /// page shows the matches of one viewer at a time. In a browser without the API, the matches are found and scrolled
    /// to but not highlighted.
    /// </remarks>
    public async Task<int> FindAsync(string text, bool matchCase = false)
    {
        ArgumentNullException.ThrowIfNull(text);
        await CallFindAsync("find", text, matchCase);
        return FindMatchCount;
    }

    /// <summary>Goes to the next match, after the last one to the first. Returns whether there is a match.</summary>
    public async Task<bool> FindNextAsync()
    {
        await CallFindAsync("findStep", 1);
        return FindMatchCount > 0;
    }

    /// <summary>Goes to the previous match, before the first one to the last. Returns whether there is a match.</summary>
    public async Task<bool> FindPreviousAsync()
    {
        await CallFindAsync("findStep", -1);
        return FindMatchCount > 0;
    }

    /// <summary>Takes the highlights of the last find away.</summary>
    public async Task ClearFindAsync()
    {
        FindMatchCount = 0;
        CurrentFindMatch = 0;
        if (await FindModuleAsync() is { } module)
        {
            await module.InvokeVoidAsync("clearFind", _root);
        }
    }

    private async Task CallFindAsync(string function, params object[] arguments)
    {
        if (await FindModuleAsync() is not { } module)
        {
            return;
        }
        try
        {
            var result = await module.InvokeAsync<int[]>(function, [_root, .. arguments]);
            (FindMatchCount, CurrentFindMatch) = (result[0], result[1]);
        }
        catch (JSException)
        {
            // The text left the page between the call and the script, as a reply that is still arriving re-renders.
            (FindMatchCount, CurrentFindMatch) = (0, 0);
        }
    }

    /// <summary>The script module, or <see langword="null"/> when the viewer is not interactive or the module failed.</summary>
    private async Task<IJSObjectReference?> FindModuleAsync()
    {
        if (!RendererInfo.IsInteractive)
        {
            return null;
        }
        try
        {
            _module ??= JS.InvokeAsync<IJSObjectReference>("import", ModulePath).AsTask();
            return await _module;
        }
        catch (JSException)
        {
            ForgetFailedImport();
        }
        catch (TaskCanceledException)
        {
            ForgetFailedImport();
        }
        return null;
    }
}
