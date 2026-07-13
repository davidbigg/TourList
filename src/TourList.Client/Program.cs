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

await builder.Build().RunAsync();
