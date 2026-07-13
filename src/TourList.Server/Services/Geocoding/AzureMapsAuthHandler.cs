using System.Net.Http.Headers;
using Azure.Core;
using Microsoft.Extensions.Options;
using TourList.Server.Options;

namespace TourList.Server.Services.Geocoding;

/// <summary>
/// Attaches Azure Maps auth to outgoing geocoding requests: a managed-identity bearer token
/// (scope <c>https://atlas.microsoft.com/.default</c>) plus the <c>x-ms-client-id</c> header,
/// or a subscription key as a local-dev fallback.
/// </summary>
public sealed class AzureMapsAuthHandler(TokenCredential credential, IOptions<AzureMapsOptions> options)
    : DelegatingHandler
{
    private static readonly string[] Scopes = ["https://atlas.microsoft.com/.default"];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var opts = options.Value;

        if (!string.IsNullOrEmpty(opts.SubscriptionKey))
        {
            var uri = request.RequestUri!;
            var separator = string.IsNullOrEmpty(uri.Query) ? "?" : "&";
            request.RequestUri = new Uri($"{uri}{separator}subscription-key={Uri.EscapeDataString(opts.SubscriptionKey)}");
        }
        else
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext(Scopes), ct);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            if (!string.IsNullOrEmpty(opts.ClientId))
            {
                request.Headers.TryAddWithoutValidation("x-ms-client-id", opts.ClientId);
            }
        }

        return await base.SendAsync(request, ct);
    }
}
