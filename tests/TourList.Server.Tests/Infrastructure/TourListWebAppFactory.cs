using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TourList.Server.Options;
using TourList.Shared.Auth;

namespace TourList.Server.Tests.Infrastructure;

/// <summary>
/// Boots the real Server against a test SQL Server database, seeding a known admin. Provides helpers
/// for the antiforgery + cookie dance so tests can act as an authenticated admin.
/// </summary>
public sealed class TourListWebAppFactory(string connectionString, string adminEmail, string adminPassword)
    : WebApplicationFactory<Program>
{
    public string AdminEmail { get; } = adminEmail;
    public string AdminPassword { get; } = adminPassword;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:tourlist", connectionString);
        builder.UseSetting($"{AdminBootstrapOptions.SectionName}:Email", AdminEmail);
        builder.UseSetting($"{AdminBootstrapOptions.SectionName}:Password", AdminPassword);
    }

    public HttpClient NewClient() =>
        CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
}

public static class TestClientExtensions
{
    /// <summary>Fetches an antiforgery request token and sets it as the default X-XSRF-TOKEN header.</summary>
    public static async Task<string> PrimeAntiforgeryAsync(this HttpClient client)
    {
        var token = await client.GetStringAsync("/api/antiforgery/token");
        client.DefaultRequestHeaders.Remove("X-XSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);
        return token;
    }

    /// <summary>Logs in as the given user and leaves the client ready for authenticated writes.</summary>
    public static async Task AuthenticateAsync(this HttpClient client, string email, string password)
    {
        await client.PrimeAntiforgeryAsync();

        var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = password });
        login.EnsureSuccessStatusCode();

        // Antiforgery tokens are bound to the caller's identity — re-mint now that we're authenticated.
        await client.PrimeAntiforgeryAsync();
    }
}
