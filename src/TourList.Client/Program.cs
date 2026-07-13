using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TourList.Client;
using TourList.Client.Auth;
using TourList.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin API (the Server hosts this WASM app) — the auth cookie rides fetches automatically.
// The AntiforgeryHandler attaches X-XSRF-TOKEN to non-GET requests.
// Singleton so it's shared across the IHttpClientFactory handler scope and the app scope —
// otherwise Clear() (after login/logout) wouldn't reach the handler's cached token.
builder.Services.AddSingleton<AntiforgeryState>();
builder.Services.AddScoped<AntiforgeryHandler>();
builder.Services.AddHttpClient("api", client => client.BaseAddress = new Uri(builder.HostEnvironment.BaseAddress))
    .AddHttpMessageHandler<AntiforgeryHandler>();
builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("api"));

// Auth
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthApi>();
builder.Services.AddScoped<TourAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<TourAuthStateProvider>());

builder.Services.AddScoped<CatalogApi>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddTransient<MapInterop>(); // component-owned JS module; disposed by Map.razor

// Route storage: auth-agnostic router → LocalRouteStore (guest) or ApiRouteStore (registered).
builder.Services.AddScoped<LocalRouteStore>();
builder.Services.AddScoped<ApiRouteStore>();
builder.Services.AddScoped<IRouteStore, RouteStoreRouter>();
builder.Services.AddScoped<RouteBuilderState>();
builder.Services.AddScoped<GuestMigrationService>();

// Driving view: offline route cache + device geolocation.
builder.Services.AddScoped<OpenedRouteCache>();
builder.Services.AddTransient<GeolocationInterop>(); // component-owned by DrivingView

await builder.Build().RunAsync();
