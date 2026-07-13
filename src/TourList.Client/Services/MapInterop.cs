using Microsoft.JSInterop;

namespace TourList.Client.Services;

/// <summary>
/// Thin wrapper over the <c>wwwroot/js/tourmap.js</c> Azure Maps module. The interop boundary is
/// coarse and passes JSON strings, not live objects. <see cref="InitAsync"/> returns false when
/// Azure Maps is not configured (token endpoint 503) so the UI can show a placeholder.
/// </summary>
public sealed class MapInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/tourmap.js");

    /// <summary>Initializes the map into <paramref name="elementId"/>. Returns true if a live map was created.</summary>
    public async Task<bool> InitAsync(string elementId, string theme)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<bool>("initMap", elementId, "api/maps/token", theme);
    }

    public async Task SetPointsAsync(string pointsJson)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("setPoints", pointsJson);
    }

    public async Task SetFilterAsync(string visibleListIdsJson)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("setFilter", visibleListIdsJson);
    }

    public async Task SetThemeAsync(string theme)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("setTheme", theme);
    }

    public async Task ZoomToDataAsync()
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("zoomToData");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("dispose");
        }
        catch (JSDisconnectedException)
        {
            // Circuit already gone (page teardown) — nothing to clean up.
        }

        await _module.DisposeAsync();
    }
}
