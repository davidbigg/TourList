using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace TourList.Server.Controllers;

/// <summary>
/// Primes the antiforgery pair for the SPA: sets the httpOnly antiforgery secret cookie and hands
/// back the request token (also mirrored into a JS-readable <c>XSRF-TOKEN</c> cookie). The client's
/// <c>AntiforgeryHandler</c> attaches the token as <c>X-XSRF-TOKEN</c> on non-GET requests.
/// Guest-reachable (writes require a token, and login/register are writes).
/// </summary>
[ApiController]
[Route("api/antiforgery")]
public sealed class AntiforgeryController(IAntiforgery antiforgery) : ControllerBase
{
    [HttpGet("token")]
    [AllowAnonymous]
    public IActionResult GetToken()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);

        Response.Cookies.Append("XSRF-TOKEN", tokens.RequestToken!, new CookieOptions
        {
            HttpOnly = false, // must be readable by client JS/interop
            Secure = true,
            SameSite = SameSiteMode.Strict,
        });

        return Content(tokens.RequestToken!, "text/plain");
    }
}
