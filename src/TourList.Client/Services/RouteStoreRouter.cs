using Microsoft.AspNetCore.Components.Authorization;
using TourList.Shared.Routes;

namespace TourList.Client.Services;

/// <summary>
/// Auth-agnostic front door for route storage: delegates to <see cref="ApiRouteStore"/> when the
/// user is authenticated, else <see cref="LocalRouteStore"/> (guest / IndexedDB). Pages depend only
/// on <see cref="IRouteStore"/> and never branch on auth state themselves.
/// </summary>
public sealed class RouteStoreRouter(
    LocalRouteStore local,
    ApiRouteStore api,
    AuthenticationStateProvider authState) : IRouteStore
{
    private async Task<IRouteStore> CurrentAsync()
    {
        var state = await authState.GetAuthenticationStateAsync();
        return state.User.Identity?.IsAuthenticated == true ? api : local;
    }

    public async Task<IReadOnlyList<RouteDto>> GetAllAsync() => await (await CurrentAsync()).GetAllAsync();

    public async Task<RouteDto?> GetAsync(Guid id) => await (await CurrentAsync()).GetAsync(id);

    public async Task SaveAsync(RouteDto route) => await (await CurrentAsync()).SaveAsync(route);

    public async Task DeleteAsync(Guid id) => await (await CurrentAsync()).DeleteAsync(id);
}
