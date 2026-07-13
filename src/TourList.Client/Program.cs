using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TourList.Client;
using TourList.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Same-origin API (the Server hosts this WASM app) — the auth cookie rides fetches automatically.
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddScoped<CatalogApi>();
builder.Services.AddScoped<ThemeService>();
builder.Services.AddTransient<MapInterop>(); // component-owned JS module; disposed by Map.razor

// Route building (guest/local for now; an auth-aware router replaces LocalRouteStore in M5).
builder.Services.AddScoped<IRouteStore, LocalRouteStore>();
builder.Services.AddScoped<RouteBuilderState>();

// Driving view: offline route cache + device geolocation.
builder.Services.AddScoped<OpenedRouteCache>();
builder.Services.AddTransient<GeolocationInterop>(); // component-owned by DrivingView

await builder.Build().RunAsync();
