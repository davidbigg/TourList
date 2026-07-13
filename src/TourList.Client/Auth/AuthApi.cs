using System.Net.Http.Json;
using TourList.Shared.Auth;

namespace TourList.Client.Auth;

/// <summary>Client for the cookie/BFF auth endpoints. The httpOnly cookie rides same-origin fetches
/// automatically; this only manages the antiforgery token lifecycle around identity changes.</summary>
public sealed class AuthApi(HttpClient http, AntiforgeryState antiforgery)
{
    public async Task<AuthUserDto?> GetMeAsync()
    {
        try
        {
            using var response = await http.GetAsync("api/auth/me");
            return response.IsSuccessStatusCode ? await response.Content.ReadFromJsonAsync<AuthUserDto>() : null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> LoginAsync(string email, string password)
    {
        using var response = await http.PostAsJsonAsync("api/auth/login", new LoginRequest { Email = email, Password = password });
        antiforgery.Clear();
        return response.IsSuccessStatusCode;
    }

    public async Task<(bool Ok, string? Error)> RegisterAsync(string email, string password)
    {
        using var response = await http.PostAsJsonAsync("api/auth/register", new RegisterRequest { Email = email, Password = password });
        antiforgery.Clear();
        return response.IsSuccessStatusCode ? (true, null) : (false, await FirstErrorAsync(response));
    }

    public async Task LogoutAsync()
    {
        using var _ = await http.PostAsync("api/auth/logout", content: null);
        antiforgery.Clear();
    }

    public async Task ForgotPasswordAsync(string email) =>
        await http.PostAsJsonAsync("api/auth/forgot-password", new ForgotPasswordRequest { Email = email });

    public async Task<bool> ResetPasswordAsync(string email, string token, string newPassword)
    {
        using var response = await http.PostAsJsonAsync("api/auth/reset-password",
            new ResetPasswordRequest { Email = email, Token = token, NewPassword = newPassword });
        return response.IsSuccessStatusCode;
    }

    private static async Task<string?> FirstErrorAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ValidationProblemLike>();
            var first = problem?.Errors?.Values.SelectMany(v => v).FirstOrDefault();
            return first ?? problem?.Title ?? "Registration failed.";
        }
        catch
        {
            return "Registration failed.";
        }
    }

    private sealed class ValidationProblemLike
    {
        public string? Title { get; set; }
        public Dictionary<string, string[]>? Errors { get; set; }
    }
}
