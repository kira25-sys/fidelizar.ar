using System.Globalization;
using System.Security.Claims;

namespace Fidelizar.Api.Security;

/// <summary>
/// Reads a caller's own identity out of the authenticated token — never out of a request body or
/// a query string. I8 says <c>NegocioId</c> is a required parameter, not a convention: a
/// controller gets it from here, from the session the JWT proves, so a cashier can never ask for
/// another business's data by typing a different id in the URL (ARCHITECTURE §8).
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static int ObtenerNegocioId(this ClaimsPrincipal principal) =>
        int.Parse(
            principal.FindFirstValue(JwtTokenService.NegocioIdClaim)
                ?? throw new InvalidOperationException($"El principal autenticado no trae '{JwtTokenService.NegocioIdClaim}'."),
            CultureInfo.InvariantCulture);

    public static int ObtenerUsuarioId(this ClaimsPrincipal principal) =>
        int.Parse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("El principal autenticado no trae NameIdentifier."),
            CultureInfo.InvariantCulture);
}
