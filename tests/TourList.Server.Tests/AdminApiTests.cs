using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using TourList.Server.Data;
using TourList.Server.Data.Entities;
using TourList.Server.Tests.Infrastructure;
using TourList.Shared.Entries;
using TourList.Shared.Enums;
using TourList.Shared.Lists;

namespace TourList.Server.Tests;

/// <summary>Admin list/entry management server-side flows (acceptance scenarios 2, 3, 10).</summary>
[Collection(SqlServerCollection.Name)]
public sealed class AdminApiTests : IDisposable
{
    private readonly TourListWebAppFactory _factory;

    public AdminApiTests(SqlServerContainerFixture fixture)
    {
        _factory = new TourListWebAppFactory(
            fixture.ConnectionStringFor("tl_admin_tests"),
            adminEmail: "admin@tourlist.local",
            adminPassword: "Admin!Passw0rd");
    }

    public void Dispose() => _factory.Dispose();

    [Fact] // scenario 2 (server side; map render is M2)
    public async Task Admin_creates_a_list_and_three_entries()
    {
        var client = _factory.NewClient();
        await client.AuthenticateAsync(_factory.AdminEmail, _factory.AdminPassword);

        var created = await client.PostAsJsonAsync("/api/lists", new CreateListRequest { Name = "Stone Option A" });
        created.EnsureSuccessStatusCode();
        var list = await created.Content.ReadFromJsonAsync<ListDto>();
        Assert.NotNull(list);

        foreach (var address in new[] { "1 Microsoft Way, Redmond", "400 Broad St, Seattle", "1600 Amphitheatre Pkwy" })
        {
            var add = await client.PostAsJsonAsync($"/api/lists/{list!.Id}/entries", new CreateEntryRequest { RawAddress = address });
            add.EnsureSuccessStatusCode();
        }

        var entries = await client.GetFromJsonAsync<List<EntryDto>>($"/api/lists/{list!.Id}/entries");
        Assert.Equal(3, entries!.Count);

        var lists = await client.GetFromJsonAsync<List<ListDto>>("/api/lists");
        Assert.Contains(lists!, l => l.Id == list.Id && l.EntryCount == 3);
    }

    [Fact] // scenario 3 (server side)
    public async Task Csv_import_reports_unparseable_rows_and_imports_the_rest()
    {
        var client = _factory.NewClient();
        await client.AuthenticateAsync(_factory.AdminEmail, _factory.AdminPassword);

        var list = await (await client.PostAsJsonAsync("/api/lists", new CreateListRequest { Name = "Imported list" }))
            .Content.ReadFromJsonAsync<ListDto>();

        const string csv = "Address,Notes\n123 Main St,note one\n456 Oak Ave,\n,orphan note with no address\n";
        using var content = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(file, "file", "import.csv");

        var response = await client.PostAsync($"/api/lists/{list!.Id}/entries/import", content);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CsvImportResult>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.Imported.Count);
        Assert.Single(result.ParseFailures);
        Assert.Equal(4, result.ParseFailures[0].RowNumber); // header=1, so the empty-address row is file row 4
        Assert.Equal(3, result.Summary.TotalRows);           // 2 imported + 1 parse failure
    }

    [Fact] // scenario 10
    public async Task Admin_resolves_a_flagged_entry_with_a_manual_pin()
    {
        Guid entryId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var list = new AddressList { Id = Guid.NewGuid(), Name = $"Flagged {Guid.NewGuid()}" };
            var entry = new Entry
            {
                Id = Guid.NewGuid(),
                ListId = list.Id,
                RawAddress = "unresolvable address ???",
                GeocodeStatus = GeocodeStatus.Unlocatable,
            };
            db.Lists.Add(list);
            db.Entries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;
        }

        var client = _factory.NewClient();
        await client.AuthenticateAsync(_factory.AdminEmail, _factory.AdminPassword);

        var flagged = await client.GetFromJsonAsync<List<EntryDto>>("/api/entries/flagged");
        Assert.Contains(flagged!, e => e.Id == entryId);

        var locate = await client.PostAsJsonAsync(
            $"/api/entries/{entryId}/locate",
            new LocateEntryRequest { Latitude = 47.6062, Longitude = -122.3321 });
        locate.EnsureSuccessStatusCode();

        var located = await locate.Content.ReadFromJsonAsync<EntryDto>();
        Assert.Equal(GeocodeStatus.Located, located!.GeocodeStatus);
        Assert.True(located.ManuallyLocated);
        Assert.Equal(47.6062, located.Latitude);
        Assert.Equal(-122.3321, located.Longitude);

        var flaggedAfter = await client.GetFromJsonAsync<List<EntryDto>>("/api/entries/flagged");
        Assert.DoesNotContain(flaggedAfter!, e => e.Id == entryId);
    }
}
