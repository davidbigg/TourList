using System.Net.Http.Json;
using TourList.Shared.Routes;

namespace TourList.Client.Services;

/// <summary>Registered-user route persistence via the server API (owner-scoped). Uses PUT as an
/// idempotent upsert keyed by the client-generated route id.</summary>
public sealed class ApiRouteStore(HttpClient http) : IRouteStore
{
    public async Task<IReadOnlyList<RouteDto>> GetAllAsync() =>
        await http.GetFromJsonAsync<List<RouteDto>>("api/routes") ?? [];

    public async Task<RouteDto?> GetAsync(Guid id)
    {
        using var response = await http.GetAsync($"api/routes/{id}");
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<RouteDto>() : null;
    }

    public async Task SaveAsync(RouteDto route)
    {
        using var response = await http.PutAsJsonAsync($"api/routes/{route.Id}", route);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAsync(Guid id) => await http.DeleteAsync($"api/routes/{id}");
}
