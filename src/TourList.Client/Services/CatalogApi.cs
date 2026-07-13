using System.Net.Http.Json;
using TourList.Shared.Entries;
using TourList.Shared.Lists;

namespace TourList.Client.Services;

/// <summary>Reads the public content catalog (lists + entries) from the server.</summary>
public sealed class CatalogApi(HttpClient http)
{
    public async Task<IReadOnlyList<ListDto>> GetListsAsync(CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<ListDto>>("api/lists", ct) ?? [];

    public async Task<IReadOnlyList<EntryDto>> GetEntriesAsync(Guid listId, CancellationToken ct = default) =>
        await http.GetFromJsonAsync<List<EntryDto>>($"api/lists/{listId}/entries", ct) ?? [];
}
