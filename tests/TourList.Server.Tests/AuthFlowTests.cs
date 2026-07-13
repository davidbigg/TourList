using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TourList.Server.Options;
using TourList.Server.Tests.Infrastructure;
using TourList.Shared;
using TourList.Shared.Auth;

namespace TourList.Server.Tests;

/// <summary>
/// Exercises the real BFF cookie + antiforgery pipeline end-to-end against the seeded admin
/// (acceptance scenario 1).
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class AuthFlowTests : IDisposable
{
    private const string AdminEmail = "admin@tourlist.local";
    private const string AdminPassword = "Admin!Passw0rd";

    private readonly Factory _factory;

    public AuthFlowTests(SqlServerContainerFixture fixture)
    {
        _factory = new Factory(fixture.ConnectionStringFor("tl_auth_tests"));
    }

    public void Dispose() => _factory.Dispose();

    private HttpClient NewClient() =>
        _factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

    [Fact]
    public async Task Seeded_admin_can_log_in_and_is_in_admin_role()
    {
        var client = NewClient();

        var tokenResp = await client.GetAsync("/api/antiforgery/token");
        var tokenBody = await tokenResp.Content.ReadAsStringAsync();
        Assert.True(tokenResp.IsSuccessStatusCode, $"token endpoint {(int)tokenResp.StatusCode}: {tokenBody}");
        var token = tokenBody;
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);

        // Wrong password → 401 (still anonymous — same antiforgery token is valid).
        var bad = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = AdminEmail, Password = "not-the-password" });
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);

        // Correct password → 200 and the returned identity carries the Admin role.
        var ok = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Email = AdminEmail, Password = AdminPassword });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var me = await ok.Content.ReadFromJsonAsync<AuthUserDto>();
        Assert.NotNull(me);
        Assert.Equal(AdminEmail, me!.Email);
        Assert.Contains(RoleNames.Admin, me.Roles);

        // The httpOnly cookie now authenticates a follow-up request.
        var meResp = await client.GetAsync("/api/auth/me");
        meResp.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Me_is_unauthorized_when_anonymous()
    {
        var client = NewClient();
        var resp = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    private sealed class Factory(string connectionString) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:tourlist", connectionString);
            builder.UseSetting($"{AdminBootstrapOptions.SectionName}:Email", AdminEmail);
            builder.UseSetting($"{AdminBootstrapOptions.SectionName}:Password", AdminPassword);
        }
    }
}
