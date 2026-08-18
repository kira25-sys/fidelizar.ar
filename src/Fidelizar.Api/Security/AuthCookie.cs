namespace Fidelizar.Api.Security;

/// <summary>
/// The one place that names the session cookie and builds its options, so login, logout and the
/// silent-renewal path in <c>AuthController</c> cannot drift from each other (ARCHITECTURE §8:
/// "It travels in an HttpOnly, Secure, SameSite=Strict cookie, not in localStorage and not in an
/// Authorization header set by JavaScript"). Pure — no <c>HttpContext</c>, no I/O — so the three
/// flags are unit-testable with no server (ARCHITECTURE §11).
/// </summary>
public static class AuthCookie
{
    public const string Name = "fidelizar_auth";

    public static CookieOptions Build(DateTime expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Expires = new DateTimeOffset(DateTime.SpecifyKind(expiresAtUtc, DateTimeKind.Utc)),
        Path = "/",
    };

    /// <summary>Used by logout — clearing a cookie must be attempted with the exact same
    /// attributes it was set with, or some browsers keep the original.</summary>
    public static CookieOptions BuildForDeletion() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
    };
}
