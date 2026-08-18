using Fidelizar.Api.Auth;
using Fidelizar.Api.Configurations;
using Fidelizar.Api.Security;
using Fidelizar.Application.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fidelizar.Api.Controllers;

/// <summary>
/// The minimal session endpoints F1-03 owns: login, logout and "quién soy" (ARCHITECTURE §8).
/// Authorising individual business endpoints per role and per branch is F1-04's job, not this
/// controller's — everything here only asks "is there a valid session at all".
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(
    IAuthService authService, IJwtTokenService jwtTokenService, IAntiforgery antiforgery) : ControllerBase
{
    /// <summary>
    /// Hands out the antiforgery token the client must echo back, in the header
    /// <see cref="AntiforgeryConfigurationExtensions.HeaderName"/> names, on every state-changing
    /// request — including login itself (ARCHITECTURE §8). Anonymous on purpose: a cashier has no
    /// session yet when they load the login screen.
    /// </summary>
    [HttpGet("csrf-token")]
    [AllowAnonymous]
    public IActionResult ObtenerTokenAntifalsificacion()
    {
        var tokens = antiforgery.GetAndStoreTokens(HttpContext);
        return Ok(new { token = tokens.RequestToken });
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitingConfigurationExtensions.LoginPolicyName)]
    [AntiforgeryTokenRequired]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var usuario = await authService.AutenticarAsync(request.Email, request.Password, cancellationToken);
        var (token, expiraUtc) = jwtTokenService.CrearToken(usuario);

        Response.Cookies.Append(AuthCookie.Name, token, AuthCookie.Build(expiraUtc));

        return Ok(SesionInfo.DeUsuario(usuario));
    }

    [HttpPost("logout")]
    [Authorize]
    [AntiforgeryTokenRequired]
    public IActionResult Logout()
    {
        Response.Cookies.Delete(AuthCookie.Name, AuthCookie.BuildForDeletion());
        return NoContent();
    }

    /// <summary>
    /// "Quién soy": the identity the current cookie carries. Also where silent renewal happens
    /// (ARCHITECTURE §8) — every authenticated call here reissues the cookie with a fresh
    /// expiration, so a cashier's client calling this on each navigation never sees the session
    /// drop mid-shift. No refresh token, no server-side session store: the JWT stays the only
    /// state, just re-signed.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public IActionResult QuienSoy()
    {
        var sesion = SesionInfo.DeClaims(User);

        var (token, expiraUtc) = jwtTokenService.RenovarToken(User);
        Response.Cookies.Append(AuthCookie.Name, token, AuthCookie.Build(expiraUtc));

        return Ok(sesion);
    }
}
