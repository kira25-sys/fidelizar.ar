using System.Globalization;
using System.Security.Claims;
using Fidelizar.Api.Security;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Auth;

/// <summary>
/// What login and "quién soy" return about the signed-in user — their own identity, never a
/// member's (no privacy concern: this is the staff member's own name and role, not customer PII).
/// Deliberately not in <c>Fidelizar.Shared</c>, same reasoning as <see cref="LoginRequest"/>.
/// </summary>
public sealed record SesionInfo(int Id, string NombreCompleto, string Rol, int NegocioId, int? SucursalId)
{
    public static SesionInfo DeUsuario(Usuario usuario) =>
        new(usuario.Id, usuario.NombreCompleto, usuario.Rol.ToString(), usuario.NegocioId, usuario.SucursalId);

    public static SesionInfo DeClaims(ClaimsPrincipal principal) => new(
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture),
        principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
        principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
        int.Parse(principal.FindFirstValue(JwtTokenService.NegocioIdClaim)!, CultureInfo.InvariantCulture),
        principal.FindFirstValue(JwtTokenService.SucursalIdClaim) is { } s
            ? int.Parse(s, CultureInfo.InvariantCulture)
            : null);
}
