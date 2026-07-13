using Microsoft.JSInterop;
using TourList.Shared.Routes;

namespace TourList.Client.Services;

/// <summary>Guest route persistence in the IndexedDB <c>guestRoutes</c> store (device-local).</summary>
public sealed class LocalRouteStore(IJSRuntime js) : IRouteStore, IAsyncDisposable
{
    private const string Store = "guestRoutes";
    private IJSObjectReference? _module;

    private async Task<IJSObjectReference> ModuleAsync() =>
        _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/idb.js");

    public async Task<IReadOnlyList<RouteDto>> GetAllAsync()
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<List<RouteDto>>("getAll", Store);
    }

    public async Task<RouteDto?> GetAsync(Guid id)
    {
        var module = await ModuleAsync();
        return await module.InvokeAsync<RouteDto?>("get", Store, id.ToString());
    }

    public async Task SaveAsync(RouteDto route)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("put", Store, route.Id.ToString(), route);
    }

    public async Task DeleteAsync(Guid id)
    {
        var module = await ModuleAsync();
        await module.InvokeVoidAsync("del", Store, id.ToString());
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }
}
