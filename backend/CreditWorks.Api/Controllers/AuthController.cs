using CreditWorks.Api.Dtos;
using CreditWorks.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CreditWorks.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    public const string CookieName = "cw_auth";

    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<SessionResponse>> Login([FromBody] LoginRequest request)
    {
        var result = await _authService.LoginAsync(request);
        if (result is null)
        {
            // Generic message — never reveals whether username or password was wrong.
            return Unauthorized(new { title = "Invalid username or password." });
        }

        var (token, session) = result.Value;

        Response.Cookies.Append(CookieName, token, new CookieOptions
        {
            HttpOnly = true,
            // Secure if-and-only-if this request actually arrived over
            // HTTPS — not "are we in Production", which is only ever an
            // imperfect proxy for that (it breaks both behind a
            // TLS-terminating proxy/load balancer in real deployments,
            // where the app itself sees plain HTTP, and in this project's
            // own WebApplicationFactory-based integration tests, which
            // talk to the in-process TestServer over HTTP regardless of
            // configured environment name). A cookie marked Secure is
            // simply dropped by the client on a non-HTTPS connection
            // either way, so tying this to the request's actual scheme is
            // both more correct and self-adjusting.
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Strict,
            Expires = session.ExpiresAtUtc,
            Path = "/"
        });

        return Ok(session);
    }

    /// <summary>
    /// Called by the frontend on mount of any admin route to decide what to
    /// render. This is a UX convenience only — the real enforcement is the
    /// [Authorize] attribute on CategoriesController, checked independently
    /// on every request.
    /// </summary>
    [HttpGet("session")]
    [Authorize(Roles = "Admin")]
    public ActionResult<SessionResponse> Session()
    {
        var username = User.FindFirst("username")?.Value ?? string.Empty;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
        var expClaim = User.FindFirst("exp")?.Value;
        var expiresAtUtc = expClaim is not null
            ? DateTimeOffset.FromUnixTimeSeconds(long.Parse(expClaim)).UtcDateTime
            : DateTime.UtcNow;

        return Ok(new SessionResponse { Username = username, Role = role, ExpiresAtUtc = expiresAtUtc });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/" });
        return Ok();
    }
}
