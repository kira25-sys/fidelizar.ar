using System.Globalization;
using System.Security.Claims;
using Fidelizar.Api.Security;
using Fidelizar.Domain.Entities;
using Fidelizar.Shared.Auth;

namespace Fidelizar.Api.Auth;

/// <summary>
/// Builds the Shared <see cref="SesionResponse"/> DTO from server-side identity sources. Stays in
/// Api, not Shared: it reads <see cref="Usuario"/> (Domain) and <see cref="ClaimsPrincipal"/> tied
/// to <see cref="JwtTokenService"/>'s claim names, neither of which Shared may reference
/// (ARCHITECTURE §3).
/// </summary>
public static class SesionResponseMapper
{
    public static SesionResponse DeUsuario(Usuario usuario) =>
        new(usuario.Id, usuario.NombreCompleto, usuario.Rol.ToString(), usuario.NegocioId, usuario.SucursalId);

    public static SesionResponse DeClaims(ClaimsPrincipal principal) => new(
        int.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!, CultureInfo.InvariantCulture),
        principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
        principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
        int.Parse(principal.FindFirstValue(JwtTokenService.NegocioIdClaim)!, CultureInfo.InvariantCulture),
        principal.FindFirstValue(JwtTokenService.SucursalIdClaim) is { } s
            ? int.Parse(s, CultureInfo.InvariantCulture)
            : null);
}
