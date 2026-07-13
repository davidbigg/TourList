using Azure.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using TourList.Server.Options;
using TourList.Shared.Maps;

namespace TourList.Server.Controllers;

/// <summary>
/// Guest-reachable Azure Maps token minting for the anonymous Web SDK flow. Because it is public it
/// is a cost-abuse surface, so it is rate-limited (per-IP) and returns short-lived tokens. Returns
/// 503 until Azure Maps is configured (see <see cref="AzureMapsOptions"/>).
/// </summary>
[ApiController]
[Route("api/maps")]
public sealed class MapsController(
    TokenCredential credential,
    IOptions<AzureMapsOptions> options,
    ILogger<MapsController> logger) : ControllerBase
{
    private static readonly string[] Scopes = ["https://atlas.microsoft.com/.default"];

    [HttpGet("token")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.MapsToken)]
    public async Task<ActionResult<MapsTokenResponse>> GetToken(CancellationToken ct)
    {
        var opts = options.Value;
        if (!opts.Enabled || string.IsNullOrWhiteSpace(opts.ClientId))
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Azure Maps is not configured.",
                detail: "Set AzureMaps:Enabled and AzureMaps:ClientId to enable the map.");
        }

        try
        {
            var token = await credential.GetTokenAsync(new TokenRequestContext(Scopes), ct);
            return Ok(new MapsTokenResponse
            {
                Token = token.Token,
                ClientId = opts.ClientId,
                ExpiresOnUtc = token.ExpiresOn.UtcDateTime,
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to mint an Azure Maps token.");
            return Problem(
                statusCode: StatusCodes.Status502BadGateway,
                title: "Could not obtain an Azure Maps token.");
        }
    }
}
