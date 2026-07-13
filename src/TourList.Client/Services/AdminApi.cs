using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using TourList.Shared.Admin;
using TourList.Shared.Entries;
using TourList.Shared.Lists;

namespace TourList.Client.Services;

/// <summary>Client for the admin endpoints (users + content). Writes carry the antiforgery token.</summary>
public sealed class AdminApi(HttpClient http)
{
    // ---- users ----
    public async Task<IReadOnlyList<AdminUserDto>> GetUsersAsync() =>
        await http.GetFromJsonAsync<List<AdminUserDto>>("api/admin/users") ?? [];

    public Task<HttpResponseMessage> PromoteAsync(Guid id) => http.PostAsync($"api/admin/users/{id}/promote", null);
    public Task<HttpResponseMessage> DemoteAsync(Guid id) => http.PostAsync($"api/admin/users/{id}/demote", null);
    public Task<HttpResponseMessage> DeactivateAsync(Guid id) => http.PostAsync($"api/admin/users/{id}/deactivate", null);
    public Task<HttpResponseMessage> ReactivateAsync(Guid id) => http.PostAsync($"api/admin/users/{id}/reactivate", null);
    public Task<HttpResponseMessage> RemoveUserAsync(Guid id) => http.DeleteAsync($"api/admin/users/{id}");

    // ---- lists ----
    public async Task<IReadOnlyList<ListDto>> GetListsAsync() =>
        await http.GetFromJsonAsync<List<ListDto>>("api/lists") ?? [];

    public async Task<ListDto?> CreateListAsync(string name)
    {
        var response = await http.PostAsJsonAsync("api/lists", new CreateListRequest { Name = name });
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<ListDto>() : null;
    }

    public Task RenameListAsync(Guid id, string name) => http.PutAsJsonAsync($"api/lists/{id}", new UpdateListRequest { Name = name });
    public Task DeleteListAsync(Guid id) => http.DeleteAsync($"api/lists/{id}");

    // ---- entries ----
    public async Task<IReadOnlyList<EntryDto>> GetEntriesAsync(Guid listId) =>
        await http.GetFromJsonAsync<List<EntryDto>>($"api/lists/{listId}/entries") ?? [];

    public async Task<EntryDto?> AddEntryAsync(Guid listId, string rawAddress, string? notes)
    {
        var response = await http.PostAsJsonAsync($"api/lists/{listId}/entries", new CreateEntryRequest { RawAddress = rawAddress, Notes = notes });
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<EntryDto>() : null;
    }

    public Task DeleteEntryAsync(Guid id) => http.DeleteAsync($"api/entries/{id}");

    public async Task<EntryDto?> LocateAsync(Guid id, double lat, double lng)
    {
        var response = await http.PostAsJsonAsync($"api/entries/{id}/locate", new LocateEntryRequest { Latitude = lat, Longitude = lng });
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<EntryDto>() : null;
    }

    public async Task<IReadOnlyList<EntryDto>> GetFlaggedAsync() =>
        await http.GetFromJsonAsync<List<EntryDto>>("api/entries/flagged") ?? [];

    public async Task<CsvImportResult?> ImportCsvAsync(Guid listId, string csv, string fileName)
    {
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", fileName);

        var response = await http.PostAsync($"api/lists/{listId}/entries/import", content);
        return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<CsvImportResult>() : null;
    }
}
