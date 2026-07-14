using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TourList.Server.Options;
using TourList.Server.Services.Email;
using TourList.Server.Tests.Infrastructure;
using TourList.Shared.Auth;

namespace TourList.Server.Tests;

/// <summary>
/// Guards against host-header injection in the password-reset link: when AcsEmail:BaseUrl is
/// configured, the emailed reset link must be built from it and never from the (attacker-controlled)
/// Host header.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class PasswordResetLinkTests : IDisposable
{
    private const string AdminEmail = "admin@tourlist.local";
    private const string AdminPassword = "Admin!Passw0rd";
    private const string BaseUrl = "https://tourlist.example.com";

    private readonly CapturingEmailDispatcher _dispatcher = new();
    private readonly Factory _factory;

    public PasswordResetLinkTests(SqlServerContainerFixture fixture)
    {
        _factory = new Factory(fixture.ConnectionStringFor("tl_pwreset_tests"), _dispatcher);
    }

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Reset_link_uses_configured_base_url_not_the_forged_host_header()
    {
        var client = _factory.CreateClient(
            new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });

        var token = await client.GetStringAsync("/api/antiforgery/token");
        client.DefaultRequestHeaders.Add("X-XSRF-TOKEN", token);

        // Simulate the attack: forge the Host header to point at an attacker-controlled domain.
        client.DefaultRequestHeaders.Host = "evil.example.com";

        var resp = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest { Email = AdminEmail });

        // Still 200 (never reveals account existence), and a link was produced for the seeded admin.
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        Assert.NotNull(_dispatcher.LastResetLink);

        // The link is built from the configured BaseUrl, not the forged Host header.
        Assert.StartsWith($"{BaseUrl}/reset-password", _dispatcher.LastResetLink);
        Assert.DoesNotContain("evil.example.com", _dispatcher.LastResetLink);
    }

    private sealed class CapturingEmailDispatcher : IEmailDispatcher
    {
        public string? LastResetLink { get; private set; }

        public Task SendPasswordResetAsync(string toEmail, string resetLink, CancellationToken ct = default)
        {
            LastResetLink = resetLink;
            return Task.CompletedTask;
        }
    }

    private sealed class Factory(string connectionString, IEmailDispatcher dispatcher)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("ConnectionStrings:tourlist", connectionString);
            builder.UseSetting($"{AdminBootstrapOptions.SectionName}:Email", AdminEmail);
            builder.UseSetting($"{AdminBootstrapOptions.SectionName}:Password", AdminPassword);
            builder.UseSetting($"{AcsEmailOptions.SectionName}:BaseUrl", BaseUrl);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailDispatcher>();
                services.AddSingleton(dispatcher);
            });
        }
    }
}
