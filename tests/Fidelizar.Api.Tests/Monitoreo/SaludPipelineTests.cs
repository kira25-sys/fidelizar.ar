using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Fidelizar.Api.Monitoreo;
using Fidelizar.Domain.Operaciones;
using Fidelizar.Domain.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Fidelizar.Api.Tests.Monitoreo;

/// <summary>
/// F1-18, ARCHITECTURE §14: the external uptime check per instance. Driven as real HTTP through
/// the actual pipeline — reading the DI registration proves nothing about what a monitor sitting
/// outside the container actually gets back.
/// </summary>
public class SaludPipelineTests
{
    private const string Instancia = "instancia-de-prueba";

    /// <summary>
    /// The whole point of splitting the two probes: while Postgres is down the process is still
    /// alive, and the owner has to be able to tell those apart from a phone.
    /// </summary>
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    public async Task El_chequeo_de_proceso_responde_200_aunque_la_base_no_responda(string ruta)
    {
        using var factory = new SaludApiFactory(baseResponde: false);
        var client = factory.CreateClient();

        var response = await client.GetAsync(ruta);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task El_chequeo_de_readiness_responde_200_cuando_la_base_responde()
    {
        using var factory = new SaludApiFactory(baseResponde: true);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var cuerpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Healthy", cuerpo.GetProperty("estado").GetString());
        Assert.Equal(
            "Healthy",
            cuerpo.GetProperty("chequeos").GetProperty(OpcionesDeSalud.ChequeoBase).GetString());
    }

    [Fact]
    public async Task El_chequeo_de_readiness_responde_503_cuando_la_base_no_responde()
    {
        using var factory = new SaludApiFactory(baseResponde: false);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        var cuerpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        Assert.Equal("Unhealthy", cuerpo.GetProperty("estado").GetString());
    }

    /// <summary>
    /// One VPS hosts every client (ARCHITECTURE §14). An alert that does not say whose instance
    /// it is costs the owner a round of guessing at 9pm.
    /// </summary>
    [Theory]
    [InlineData("/health")]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Toda_respuesta_de_salud_identifica_la_instancia(string ruta)
    {
        using var factory = new SaludApiFactory(baseResponde: true);
        var client = factory.CreateClient();

        var response = await client.GetAsync(ruta);
        var cuerpo = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

        Assert.Equal(Instancia, cuerpo.GetProperty("instancia").GetString());
    }

    /// <summary>
    /// The three probes are anonymous (F1-04), so their body is world-readable. It must never
    /// carry an exception, a stack trace or the connection string (CLAUDE.md).
    /// </summary>
    [Fact]
    public async Task La_respuesta_de_readiness_caida_no_filtra_la_cadena_de_conexion()
    {
        using var factory = new SaludApiFactory(baseResponde: false);
        var client = factory.CreateClient();

        var cuerpo = await (await client.GetAsync("/health/ready")).Content.ReadAsStringAsync();

        Assert.DoesNotContain("Password", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Host=", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Exception", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ARCHITECTURE §14 wants the outage to reach a phone. Which service sends it is still open
    /// (docs/OPERACION-MONITOREO.md §5); what this pins down is that the seam is actually called,
    /// so swapping the implementation is all that decision will cost.
    /// </summary>
    [Fact]
    public async Task La_base_caida_dispara_la_alerta_operativa()
    {
        using var factory = new SaludApiFactory(baseResponde: false);
        var client = factory.CreateClient();

        await client.GetAsync("/health/ready");

        var alerta = Assert.Single(factory.Alertas.Recibidas);
        Assert.Equal(ChequeoBaseDeDatos.CodigoAlerta, alerta);
    }

    [Fact]
    public async Task La_base_sana_no_dispara_ninguna_alerta()
    {
        using var factory = new SaludApiFactory(baseResponde: true);
        var client = factory.CreateClient();

        await client.GetAsync("/health/ready");

        Assert.Empty(factory.Alertas.Recibidas);
    }

    /// <summary>
    /// Boots the real pipeline with the persistence probe and the alert seam replaced, so the
    /// "database is down" branch is exercised without a Postgres. Same "Testing" environment as
    /// <c>RateLimiterPipelineTests</c>, so <c>Program.cs</c> skips the EF Core migration.
    /// </summary>
    private sealed class SaludApiFactory(bool baseResponde) : WebApplicationFactory<Program>
    {
        public AlertaFalsa Alertas { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseSetting("Monitoreo:Instancia", Instancia);
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO");
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IPersistenceProbe>();
                services.AddScoped<IPersistenceProbe>(_ => new SondaFalsa(baseResponde));

                services.RemoveAll<IAlertaOperativa>();
                services.AddSingleton<IAlertaOperativa>(Alertas);
            });
        }
    }

    private sealed class SondaFalsa(bool responde) : IPersistenceProbe
    {
        public Task<bool> RespondeAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(responde);
    }

    private sealed class AlertaFalsa : IAlertaOperativa
    {
        private readonly List<string> _recibidas = [];

        public IReadOnlyList<string> Recibidas
        {
            get { lock (_recibidas) { return _recibidas.ToList(); } }
        }

        public Task AlertarAsync(string codigo, string detalle, CancellationToken cancellationToken = default)
        {
            lock (_recibidas) { _recibidas.Add(codigo); }
            return Task.CompletedTask;
        }
    }
}
