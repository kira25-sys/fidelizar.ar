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

    /// <summary>
    /// The caller's own branch, or <c>null</c> for <c>Dueno</c>/<c>Soporte</c> — DATA-MODEL §2:
    /// <c>SucursalId</c> is required on <c>Usuario</c> for <c>Cajero</c>/<c>Encargada</c>, null
    /// for the roles that see every branch. <see cref="JwtTokenService"/> only sets the claim when
    /// the user has a branch, so a missing claim here is that same "no branch" case, not an error.
    /// </summary>
    public static int? ObtenerSucursalId(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(JwtTokenService.SucursalIdClaim) is { } valor
            ? int.Parse(valor, CultureInfo.InvariantCulture)
            : null;

    /// <summary>
    /// The branch axis of F1-04 (roadmap): a user tied to one branch operates only on that
    /// branch's own resources (e.g. S9 cierre diario) — never on another branch's. This is about
    /// which resources a user may operate, not about which member they may serve: RN-07 and
    /// FUNCTIONAL-SPEC are explicit that a member from another branch is served normally, since
    /// the program's target is global, not per-branch. <c>Dueno</c>/<c>Soporte</c> carry no
    /// branch claim (see <see cref="ObtenerSucursalId"/>) and this always returns true for them.
    /// </summary>
    public static bool PuedeOperarSucursal(this ClaimsPrincipal principal, int sucursalId) =>
        principal.ObtenerSucursalId() is not { } propia || propia == sucursalId;
}
