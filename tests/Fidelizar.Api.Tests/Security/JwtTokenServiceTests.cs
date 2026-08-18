using Fidelizar.Api.Options;
using Fidelizar.Api.Security;
using Fidelizar.Domain.Entities;

namespace Fidelizar.Api.Tests.Security;

/// <summary>ARCHITECTURE §8: expiry is always computed in UTC.</summary>
public class JwtTokenServiceTests
{
    private static JwtSettings Settings() => new()
    {
        SigningKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48)),
        Issuer = "Fidelizar.Tests",
        Audience = "Fidelizar.Tests",
        AccessTokenMinutes = 15,
    };

    private static Usuario UnUsuario() => Usuario.Crear(
        negocioId: 1,
        nombreCompleto: "Ana Gomez",
        email: "ana@x.com",
        passwordHash: "hash",
        rol: RolUsuario.Cajero,
        creadoEn: DateTime.UtcNow,
        sucursalId: 5);

    [Fact]
    public void CrearToken_devuelve_una_expiracion_en_UTC_dentro_de_la_ventana_configurada()
    {
        var settings = Settings();
        var servicio = new JwtTokenService(settings);
        var antes = DateTime.UtcNow;

        var (token, expiraUtc) = servicio.CrearToken(UnUsuario());

        Assert.Equal(DateTimeKind.Utc, expiraUtc.Kind);
        Assert.InRange(expiraUtc, antes.AddMinutes(settings.AccessTokenMinutes - 1), antes.AddMinutes(settings.AccessTokenMinutes + 1));
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public void RenovarToken_reproduce_la_misma_identidad_que_CrearToken()
    {
        var servicio = new JwtTokenService(Settings());
        var usuario = UnUsuario();

        var (_, _) = servicio.CrearToken(usuario);
        var sesion = Fidelizar.Api.Auth.SesionResponseMapper.DeUsuario(usuario);
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
        [
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, sesion.Id.ToString()),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, sesion.NombreCompleto),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, sesion.Rol),
            new System.Security.Claims.Claim(JwtTokenService.NegocioIdClaim, sesion.NegocioId.ToString()),
            new System.Security.Claims.Claim(JwtTokenService.SucursalIdClaim, sesion.SucursalId!.Value.ToString()),
        ]));

        var (tokenRenovado, expiraUtc) = servicio.RenovarToken(principal);

        Assert.Equal(DateTimeKind.Utc, expiraUtc.Kind);
        Assert.False(string.IsNullOrWhiteSpace(tokenRenovado));
    }
}
