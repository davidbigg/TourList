using TourList.Shared.Routes;

namespace TourList.Client.Services;

/// <summary>
/// Auth-agnostic route persistence. Pages depend on this; DI binds it to <see cref="LocalRouteStore"/>
/// (guest / IndexedDB) today, and to an auth-aware router (guest vs. API) once accounts land in M5.
/// </summary>
public interface IRouteStore
{
    Task<IReadOnlyList<RouteDto>> GetAllAsync();
    Task<RouteDto?> GetAsync(Guid id);
    Task SaveAsync(RouteDto route);
    Task DeleteAsync(Guid id);
}
