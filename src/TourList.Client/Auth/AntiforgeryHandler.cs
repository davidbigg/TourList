namespace TourList.Client.Auth;

/// <summary>
/// Attaches the <c>X-XSRF-TOKEN</c> header to non-GET requests (the framework's
/// AuthorizationMessageHandler is OIDC-only and unsuitable here). The token is fetched lazily from
/// the server's antiforgery endpoint and cached; it's cleared by the auth flows on identity change.
/// </summary>
public sealed class AntiforgeryHandler(AntiforgeryState state) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (IsSafe(request.Method))
        {
            return await base.SendAsync(request, ct);
        }

        var token = state.Token ??= await FetchTokenAsync(request.RequestUri!, ct);
        if (!string.IsNullOrEmpty(token))
        {
            request.Headers.TryAddWithoutValidation("X-XSRF-TOKEN", token);
        }

        return await base.SendAsync(request, ct);
    }

    private async Task<string?> FetchTokenAsync(Uri reference, CancellationToken ct)
    {
        // GET bypasses the token logic, so calling base directly here is safe (no recursion).
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(reference, "/api/antiforgery/token"));
        using var response = await base.SendAsync(request, ct);
        return response.IsSuccessStatusCode ? await response.Content.ReadAsStringAsync(ct) : null;
    }

    private static bool IsSafe(HttpMethod method) =>
        method == HttpMethod.Get || method == HttpMethod.Head || method == HttpMethod.Options || method == HttpMethod.Trace;
}
