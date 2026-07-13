namespace TourList.Client.Auth;

/// <summary>Holds the current antiforgery request token, shared by the handler and auth flows.
/// Cleared on login/logout/register so the next write re-fetches a token for the new identity.</summary>
public sealed class AntiforgeryState
{
    public string? Token { get; set; }

    public void Clear() => Token = null;
}
