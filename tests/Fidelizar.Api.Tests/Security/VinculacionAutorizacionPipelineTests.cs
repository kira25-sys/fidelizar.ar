using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Fidelizar.Api.Security;
using Fidelizar.Shared.Miembros;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// ARCHITECTURE §8: "an endpoint must reject a cashier asking for a phone number even when no
/// screen offers it". F1-14's two endpoints are <c>Encargada</c>/<c>Dueño</c> only (ROADMAP), and
/// this drives real HTTP through <c>Fidelizar.Api</c>'s actual pipeline with a Cajero's session
/// cookie — a controller-level attribute check cannot prove the 403 actually happens.
///
/// No database is involved: authorisation runs in <c>UseAuthorization</c>, before MVC ever
/// resolves the controller, so the request is rejected without reaching a repository.
/// </summary>
public class VinculacionAutorizacionPipelineTests
{
    // Generated per test run, never written to a configuration file (CLAUDE.md, ARCHITECTURE §8).
    private static readonly string SigningKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    /// <summary>
    /// The negative only. That <c>Encargada</c>/<c>Dueño</c> satisfy
    /// <see cref="Policies.EncargadaOrAbove"/> is already proved by
    /// <see cref="RolePolicyMappingTests"/> against the registered policy; asserting it here would
    /// need the database this host deliberately does not have.
    /// </summary>
    [Fact]
    public async Task Un_Cajero_que_llama_la_lista_de_socios_sin_vincular_recibe_403()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{AuthCookie.Name}={CrearToken(Roles.Cajero)}");

        var response = await client.GetAsync("/api/miembros/sin-vincular");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Un_Cajero_que_llama_el_endpoint_de_vinculacion_directo_recibe_403()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{AuthCookie.Name}={CrearToken(Roles.Cajero)}");

        var response = await client.PostAsJsonAsync(
            "/api/miembros/42/vinculacion", new VincularClienteExternoRequest("POS-100"));

        // 403 and not 400: the role gate runs in UseAuthorization, ahead of the antiforgery
        // filter, so a Cajero is refused before the missing CSRF token is even considered.
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Un_anonimo_recibe_401_en_los_dos_endpoints_de_F1_14()
    {
        using var factory = new ApiFactory();
        var client = factory.CreateClient();

        var lista = await client.GetAsync("/api/miembros/sin-vincular");
        var vinculacion = await client.PostAsJsonAsync(
            "/api/miembros/42/vinculacion", new VincularClienteExternoRequest("POS-100"));

        Assert.Equal(HttpStatusCode.Unauthorized, lista.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, vinculacion.StatusCode);
    }

    private static string CrearToken(string rol)
    {
        var ahoraUtc = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = "Fidelizar",
            Audience = "Fidelizar",
            NotBefore = ahoraUtc,
            Expires = ahoraUtc.AddMinutes(15),
            Claims = new Dictionary<string, object>
            {
                [ClaimTypes.NameIdentifier] = "3",
                [ClaimTypes.Name] = "Usuaria De Prueba",
                [ClaimTypes.Role] = rol,
                [JwtTokenService.NegocioIdClaim] = "7",
            },
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)), SecurityAlgorithms.HmacSha256),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Same "Testing" hosting environment as <see cref="RateLimiterPipelineTests"/> and
    /// for the same reason — CI has no Postgres, and this test is only about the pipeline. The
    /// connection string is a placeholder that is never opened by an unauthorised request.</summary>
    private sealed class ApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO");
            builder.UseSetting("Jwt:SigningKey", SigningKey);
        }
    }
}
