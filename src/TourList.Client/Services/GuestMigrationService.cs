using System.Net.Http.Json;
using Microsoft.JSInterop;
using TourList.Shared.Routes;

namespace TourList.Client.Services;

/// <summary>
/// One-time guest→account migration, run right after the first successful authentication: if this
/// device has guest routes and hasn't migrated yet, POST them to <c>/api/routes/merge</c>; on
/// success clear the local guest store and set a per-device flag. On failure, keep local + retry later.
/// (The server also gates by <c>GuestRoutesMigratedUtc</c>, so the account merges only once.)
/// </summary>
public sealed class GuestMigrationService(LocalRouteStore local, HttpClient http, IJSRuntime js)
{
    private const string MetaStore = "meta";
    private const string MigratedKey = "migrationDone";

    private IJSObjectReference? _idb;

    private async Task<IJSObjectReference> IdbAsync() =>
        _idb ??= await js.InvokeAsync<IJSObjectReference>("import", "./js/idb.js");

    public async Task MigrateIfNeededAsync()
    {
        var idb = await IdbAsync();

        var alreadyDone = await idb.InvokeAsync<bool?>("get", MetaStore, MigratedKey);
        if (alreadyDone == true)
        {
            return;
        }

        var guestRoutes = await local.GetAllAsync();
        if (guestRoutes.Count == 0)
        {
            await idb.InvokeVoidAsync("put", MetaStore, MigratedKey, true);
            return;
        }

        try
        {
            using var response = await http.PostAsJsonAsync(
                "api/routes/merge", new MergeRoutesRequest { Routes = guestRoutes.ToList() });

            if (response.IsSuccessStatusCode)
            {
                await local.ClearAllAsync();
                await idb.InvokeVoidAsync("put", MetaStore, MigratedKey, true);
            }
        }
        catch
        {
            // Keep local routes; retry on a later authentication.
        }
    }
}
