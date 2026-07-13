using Microsoft.JSInterop;

namespace TourList.Client.Services;

/// <summary>
/// Wraps <c>wwwroot/js/geo.js</c>. Live position is delivered to the caller's [JSInvokable]
/// <c>OnPositionChanged</c>/<c>OnPositionError</c> methods and never leaves the device.
/// </summary>
public sealed class GeolocationInterop(IJSRuntime js) : IAsyncDisposable
{
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/geo.js");

    public async Task<string> PlatformAsync()
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<string>("platform");
    }

    public async Task StartWatchAsync<T>(DotNetObjectReference<T> callbackTarget) where T : class
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("startWatch", callbackTarget);
    }

    public async Task StopWatchAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("stopWatch");
        }
        catch (JSDisconnectedException)
        {
        }
    }

    public async Task OpenExternalAsync(string url)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("openExternal", url);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.InvokeVoidAsync("stopWatch");
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
