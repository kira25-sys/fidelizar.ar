using System.Net;
using System.Security.Cryptography;
using Fidelizar.Api.Tests.Security.Fakes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Fidelizar.Api.Tests.Security;

/// <summary>
/// F1-04 (roadmap), ARCHITECTURE §8: proves the fallback policy from
/// <c>AddAppAuthentication</c> is actually wired into the real HTTP pipeline, not merely
/// registered in <c>AuthorizationOptions</c> — the same distinction
/// <see cref="RateLimiterPipelineTests"/> draws for the rate limiter ("configured but never added
/// to the request pipeline protects nothing while looking like it does").
///
/// <see cref="SinAtributoTestController"/> is wired in only for this test, via an
/// <see cref="AssemblyPart"/> added to the real host — it never ships in <c>Fidelizar.Api</c>. It
/// stands in for "a controller action someone forgot to attribute": if the fallback policy is
/// really in the pipeline, an anonymous request to it is rejected exactly like a request to any
/// endpoint that plainly requires a role would be.
/// </summary>
public class FallbackPolicyPipelineTests
{
    [Fact]
    public async Task Un_endpoint_sin_Authorize_ni_AllowAnonymous_rechaza_al_anonimo()
    {
        using var factory = new ApiFactoryConSinAtributo();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/test/sin-atributo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Un_endpoint_marcado_AllowAnonymous_sigue_respondiendo_bajo_la_misma_politica_de_reserva()
    {
        // Contrast case: the fallback policy closes what is unattributed, it does not blanket-
        // block the pipeline. `csrf-token` is explicitly `[AllowAnonymous]` (F1-03) and must keep
        // working unauthenticated.
        using var factory = new ApiFactoryConSinAtributo();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/csrf-token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>Same "Testing" hosting environment as <see cref="RateLimiterPipelineTests"/>, for
    /// the same reason — CI has no Postgres, and this test is only about the HTTP pipeline.
    /// Additionally wires <see cref="SinAtributoTestController"/> into the real
    /// <c>ApplicationPartManager</c> so <c>MapControllers</c> actually routes to it.</summary>
    private sealed class ApiFactoryConSinAtributo : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));

            builder.ConfigureServices(services =>
            {
                services.AddControllers().ConfigureApplicationPartManager(manager =>
                    manager.ApplicationParts.Add(new AssemblyPart(typeof(SinAtributoTestController).Assembly)));
            });
        }
    }
}
