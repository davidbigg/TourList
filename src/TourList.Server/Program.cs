using Azure.Core;
using Azure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TourList.Server.Data;
using TourList.Server.Options;
using TourList.Server.Services;
using TourList.Server.Services.Geocoding;

var builder = WebApplication.CreateBuilder(args);

// Aspire: OpenTelemetry, health checks, HttpClient resilience, service discovery.
builder.AddServiceDefaults();

// Aspire injects the "tourlist" connection string (SQL Server container locally / Azure SQL in prod).
builder.AddSqlServerDbContext<AppDbContext>("tourlist");

// Initial-admin bootstrap config (optional: only consumed by DbSeeder when no admin exists yet).
builder.Services.AddOptions<AdminBootstrapOptions>()
    .Bind(builder.Configuration.GetSection(AdminBootstrapOptions.SectionName));

// ---- Identity (cookie / BFF; deliberately NOT MapIdentityApi / JWT) ----
builder.Services
    .AddAuthentication(IdentityConstants.ApplicationScheme)
    .AddIdentityCookies();

builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;   // email is the login identifier
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole<Guid>>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

// Server-side session revocation: reject cookies whose security stamp changed, within 5 minutes.
builder.Services.Configure<SecurityStampValidatorOptions>(o =>
    o.ValidationInterval = TimeSpan.FromMinutes(5));

// API-friendly cookie: httpOnly + Secure + SameSite=Strict; 401/403 instead of login redirects.
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "TourList.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;

    // Preserve the security-stamp OnValidatePrincipal wired by AddIdentityCookies — only override redirects.
    options.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ActiveUser", policy => policy.RequireAuthenticatedUser());

// Antiforgery on all unsafe verbs (double-submit; token via X-XSRF-TOKEN header).
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-XSRF-TOKEN";
    options.Cookie.Name = "TourList.Antiforgery";
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

// AddControllersWithViews (not AddControllers) so the MVC antiforgery filter services in
// ViewFeatures are registered for the global AutoValidateAntiforgeryToken filter below.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// ---- Azure Maps geocoding (disabled until configured; see AzureMapsOptions) ----
builder.Services.AddOptions<AzureMapsOptions>()
    .Bind(builder.Configuration.GetSection(AzureMapsOptions.SectionName));
builder.Services.AddSingleton<TokenCredential>(_ => new DefaultAzureCredential());
builder.Services.AddTransient<AzureMapsAuthHandler>();
builder.Services.AddHttpClient<IGeocoder, AzureMapsGeocoder>((sp, client) =>
{
    var maps = sp.GetRequiredService<IOptions<AzureMapsOptions>>().Value;
    client.BaseAddress = new Uri(maps.GeocodingEndpoint);
}).AddHttpMessageHandler<AzureMapsAuthHandler>();

builder.Services.AddScoped<EntryService>();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // WebApplication adds the developer exception page automatically in Development.
    app.MapOpenApi();
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler();  // RFC 7807 ProblemDetails
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve the Blazor WASM client from this same origin (no CORS for app↔API).
app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapDefaultEndpoints();      // /health + /alive (development)
app.MapFallbackToFile("index.html");

// Apply migrations and bootstrap the initial admin (scenario 1).
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

app.Run();

/// <summary>Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> can bootstrap the app in tests.</summary>
public partial class Program;
