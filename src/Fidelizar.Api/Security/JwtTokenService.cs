using System.Globalization;
using System.Security.Claims;
using System.Text;
using Fidelizar.Api.Options;
using Fidelizar.Domain.Entities;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fidelizar.Api.Security;

/// <summary>See <see cref="IJwtTokenService"/>.</summary>
public sealed class JwtTokenService(JwtSettings settings) : IJwtTokenService
{
    public const string NegocioIdClaim = "negocio_id";
    public const string SucursalIdClaim = "sucursal_id";

    public (string Token, DateTime ExpiresAtUtc) CrearToken(Usuario usuario) =>
        CrearTokenInterno(
            usuario.Id, usuario.NombreCompleto, usuario.Rol.ToString(), usuario.NegocioId, usuario.SucursalId);

    public (string Token, DateTime ExpiresAtUtc) RenovarToken(ClaimsPrincipal principal)
    {
        var id = int.Parse(
            principal.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? throw new InvalidOperationException("El principal autenticado no trae NameIdentifier."),
            CultureInfo.InvariantCulture);
        var nombre = principal.FindFirstValue(ClaimTypes.Name) ?? string.Empty;
        var rol = principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        var negocioId = int.Parse(
            principal.FindFirstValue(NegocioIdClaim)
                ?? throw new InvalidOperationException($"El principal autenticado no trae '{NegocioIdClaim}'."),
            CultureInfo.InvariantCulture);
        var sucursalId = principal.FindFirstValue(SucursalIdClaim) is { } s
            ? int.Parse(s, CultureInfo.InvariantCulture)
            : (int?)null;

        return CrearTokenInterno(id, nombre, rol, negocioId, sucursalId);
    }

    private (string, DateTime) CrearTokenInterno(
        int id, string nombreCompleto, string rol, int negocioId, int? sucursalId)
    {
        // ARCHITECTURE §8: expiry is always computed in UTC — local time here would shift every
        // token's lifetime by the machine's offset.
        var ahoraUtc = DateTime.UtcNow;
        var expiraUtc = ahoraUtc.AddMinutes(settings.AccessTokenMinutes);

        var claims = new Dictionary<string, object>
        {
            [ClaimTypes.NameIdentifier] = id.ToString(CultureInfo.InvariantCulture),
            [ClaimTypes.Name] = nombreCompleto,
            [ClaimTypes.Role] = rol,
            [NegocioIdClaim] = negocioId.ToString(CultureInfo.InvariantCulture),
        };

        if (sucursalId is { } valorSucursal)
        {
            claims[SucursalIdClaim] = valorSucursal.ToString(CultureInfo.InvariantCulture);
        }

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            NotBefore = ahoraUtc,
            Expires = expiraUtc,
            Claims = claims,
            SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);

        return (token, expiraUtc);
    }
}
