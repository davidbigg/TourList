using Microsoft.JSInterop;
using TourList.Shared.Routes;

namespace TourList.Client.Services;

/// <summary>
/// The offline driving cache. Opening a route writes its full aggregate (all stop snapshots +
/// start point) to the IndexedDB <c>openedRoutes</c> store; the Driving View then reads <b>only</b>
/// from here, so it keeps working when the network drops (acceptance scenario 7).
/// </summary>
public sealed class OpenedRouteCache(IJSRuntime js) : IAsyncDisposable
{
    private const string Store = "openedRoutes";
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/idb.js");

    public async Task CacheAsync(RouteDto route)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("put", Store, route.Id.ToString(), route);
    }

    public async Task<RouteDto?> GetAsync(Guid id)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<RouteDto?>("get", Store, id.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is null)
        {
            return;
        }

        try
        {
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException)
        {
        }
    }
}
