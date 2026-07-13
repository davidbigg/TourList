using Microsoft.JSInterop;

namespace TourList.Client.Services;

/// <summary>
/// App-level day/night theme state (the design's shared <c>themeMode</c>). Persists to localStorage
/// and defaults from the OS <c>prefers-color-scheme</c>. Components subscribe to <see cref="Changed"/>.
/// </summary>
public sealed class ThemeService(IJSRuntime js)
{
    public const string Dark = "dark";
    public const string Light = "light";

    /// <summary>Current mode; drives the <c>.app.dark</c>/<c>.app.light</c> token set.</summary>
    public string Mode { get; private set; } = Dark;

    public string CssClass => Mode == Light ? Light : Dark;

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        var stored = await js.InvokeAsync<string?>("tourTheme.get");
        Mode = stored is Light or Dark
            ? stored
            : await js.InvokeAsync<string>("tourTheme.preferred");
        await js.InvokeVoidAsync("tourTheme.set", Mode);
        Changed?.Invoke();
    }

    public Task ToggleAsync() => SetAsync(Mode == Dark ? Light : Dark);

    public async Task SetAsync(string mode)
    {
        Mode = mode == Light ? Light : Dark;
        await js.InvokeVoidAsync("tourTheme.set", Mode);
        Changed?.Invoke();
    }
}
