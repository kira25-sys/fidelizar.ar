using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fidelizar.Api.Security;
using Fidelizar.Api.Tests.Security.Fakes;
using Fidelizar.Application.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// Boots <c>Fidelizar.Api</c>'s real pipeline — authentication, authorization and antiforgery all
/// in the middle — so F1-15's matrix is measured against HTTP, never against a controller method.
/// Calling the action directly would skip <c>[Authorize]</c>, which is the only thing under test.
///
/// Same "Testing" hosting environment as <c>RateLimiterPipelineTests</c> and for the same reason:
/// CI has no Postgres. Every Application service is replaced by a stub, so an authorised call
/// answers 2xx instead of failing on a connection and hiding the status this matrix is about.
/// </summary>
public sealed class MatrizDePermisosApiFactory : WebApplicationFactory<Program>
{
    /// <summary>Generated per test run, never written to a configuration file (CLAUDE.md,
    /// ARCHITECTURE §8).</summary>
    private readonly string signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>A session cookie for <paramref name="rol"/>. Built here rather than by logging in:
    /// the matrix must be able to forge any role's request the way an attacker would — by hand,
    /// with no screen involved.</summary>
    public string CookieDeSesion(string rol) => $"{AuthCookie.Name}={CrearToken(rol)}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting(
            "ConnectionStrings:DefaultConnection",
            "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO");
        builder.UseSetting("Jwt:SigningKey", signingKey);

        // The matrix fires a few hundred requests at one host, and TestServer puts them all in the
        // same "unknown"-IP partition: the default 100/60s global limiter would start answering
        // 429 mid-run and hide the 401/403/2xx under test. The limiter's own proof is
        // RateLimiterPipelineTests — nothing is weakened here except the noise.
        builder.UseSetting("RateLimiting:PermitLimit", "100000");
        builder.UseSetting("RateLimiting:Login:PermitLimit", "100000");

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Scoped<IAuthService, AuthServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<ISaldoService, SaldoServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<ICorteService, CorteServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<IMiembroBusquedaService, MiembroBusquedaServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<IFichaMostradorService, FichaMostradorServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<IFichaCompletaService, FichaCompletaServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<IHistorialMovimientosService, HistorialMovimientosServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<IAnulacionMovimientoService, AnulacionMovimientoServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<ICierreDiarioService, CierreDiarioServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<IUsuarioService, UsuarioServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<ISucursalService, SucursalServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<IAltaMiembroService, AltaMiembroServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<IConsentimientoTextoService, ConsentimientoTextoServiceDeLaMatriz>());
            services.Replace(ServiceDescriptor.Scoped<IVinculacionMiembroService, VinculacionMiembroServiceDeLaMatriz>());
        });
    }

    /// <summary>
    /// A token signed with this host's own key. <c>sucursal_id</c> only for Cajero and Encargada
    /// (DATA-MODEL §2) — Dueño and Soporte carry no branch, which is what lets a Dueño ask for any
    /// branch's cierre diario.
    /// </summary>
    private string CrearToken(string rol)
    {
        var ahoraUtc = DateTime.UtcNow;

        var claims = new Dictionary<string, object>
        {
            [ClaimTypes.NameIdentifier] = "3",
            [ClaimTypes.Name] = "Usuaria Ficticia De Prueba",
            [ClaimTypes.Role] = rol,
            [JwtTokenService.NegocioIdClaim] = DatosFicticiosDeLaMatriz.NegocioId.ToString(),
        };

        if (rol is Roles.Cajero or Roles.Encargada)
        {
            claims[JwtTokenService.SucursalIdClaim] = MatrizDePermisos.SucursalDelPersonal.ToString();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Fidelizar",
            Audience = "Fidelizar",
            NotBefore = ahoraUtc,
            Expires = ahoraUtc.AddMinutes(15),
            Claims = claims,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
