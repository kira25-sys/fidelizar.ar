namespace Fidelizar.Api.Security;

/// <summary>
/// Pure decision logic for silent token renewal (ARCHITECTURE §8: "Token lifetime is short and
/// renewal is silent, so a cashier is never logged out mid-flow"). Separated from
/// <c>AuthController</c>/<c>JwtTokenService</c> so the policy itself — "less than half the
/// lifetime remains" — is unit-testable with no server, no cookie and no HTTP clock
/// (ARCHITECTURE §11).
/// </summary>
public static class TokenRenewalPolicy
{
    public static bool DebeRenovar(DateTime expiraUtc, DateTime ahoraUtc, int accessTokenMinutes) =>
        expiraUtc - ahoraUtc <= TimeSpan.FromMinutes(accessTokenMinutes) / 2;
}
