using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fidelizar.Api.Tests;

/// <summary>
/// ARCHITECTURE §3 "One deployable unit": Api serves the compiled WebAssembly client as static
/// files from the same origin. Reading Program.cs by eye cannot prove that — the client's assets
/// only reach Api's wwwroot through a ProjectReference, and the F1-04 fallback authorization
/// policy would answer 401 to every one of them if the static endpoints were not AllowAnonymous.
/// These tests drive real HTTP through the actual pipeline instead.
/// </summary>
public class ClientHostingPipelineTests
{
    [Fact]
    public async Task La_raiz_devuelve_el_index_del_cliente_sin_sesion()
    {
        using var factory = new ClientHostingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("_framework/blazor.webassembly", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The client routes /ingreso itself; the server has no such endpoint. Without the SPA
    /// fallback a cashier who reloads the page — or opens a bookmark — gets a 404 instead of the
    /// login screen.
    /// </summary>
    [Theory]
    [InlineData("/ingreso")]
    [InlineData("/socios/buscar")]
    public async Task Una_ruta_del_cliente_devuelve_el_index_para_que_Blazor_la_resuelva(string path)
    {
        using var factory = new ClientHostingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// The boot script and the runtime live under _framework/ and are produced by the client's
    /// build, not committed anywhere. If the ProjectReference in Fidelizar.Api.csproj is removed,
    /// this is the test that notices.
    /// </summary>
    [Theory]
    [InlineData("/_framework/blazor.webassembly.js", "text/javascript")]
    [InlineData("/_framework/dotnet.js", "text/javascript")]
    [InlineData("/_framework/Fidelizar.Client.wasm", "application/wasm")]
    public async Task Los_archivos_del_framework_se_sirven_con_su_content_type(
        string path,
        string expectedContentType)
    {
        using var factory = new ClientHostingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedContentType, response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// The ICU data file the default extension-to-content-type table does not know. It is the
    /// reason this pipeline uses MapStaticAssets, which reads the build-time manifest, instead of
    /// UseStaticFiles, which would refuse to serve an unknown extension.
    /// </summary>
    [Fact]
    public async Task Los_datos_de_globalizacion_se_sirven_aunque_dat_no_sea_una_extension_conocida()
    {
        using var factory = new ClientHostingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/_framework/icudt_EFIGS.dat");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// The fallback must be the last endpoint considered. An unknown /api path has to keep
    /// answering as an API — a 404 or a 401 — never index.html with a 200, which would turn every
    /// typo in the client's API client into a silent parse error instead of a visible failure.
    /// </summary>
    [Fact]
    public async Task Una_ruta_de_api_desconocida_no_cae_en_el_index()
    {
        using var factory = new ClientHostingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/no-existe");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.StartsWith("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("NOT_FOUND", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// The other half of the same guarantee: a real API route still reaches its controller with
    /// the SPA fallback in place. csrf-token is the one endpoint that is both real and
    /// [AllowAnonymous] (F1-03), so a 200 here can only come from the controller.
    /// </summary>
    [Fact]
    public async Task Una_ruta_de_api_real_sigue_llegando_a_su_controlador()
    {
        using var factory = new ClientHostingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/auth/csrf-token");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("text/html", response.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    /// <summary>
    /// The fallback answers index.html to anyone, by necessity. That must not become a way to read
    /// data without a session: an authenticated API endpoint still has to reject the anonymous
    /// caller rather than fall through to the SPA with a 200.
    /// </summary>
    [Fact]
    public async Task Un_endpoint_con_sesion_obligatoria_sigue_rechazando_al_anonimo()
    {
        using var factory = new ClientHostingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/miembros/1/saldo");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// /health is mapped explicitly and must keep winning over the fallback (F1-04): a monitoring
    /// probe that received 200 text/html would report the app healthy no matter what.
    /// </summary>
    [Fact]
    public async Task El_health_check_sigue_ganandole_al_fallback()
    {
        using var factory = new ClientHostingApiFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("text/html", response.Content.Headers.ContentType?.MediaType ?? string.Empty);
    }

    /// <summary>
    /// Same "Testing" environment as <see cref="RateLimiterPipelineTests"/> — Program.cs skips the
    /// EF Core migration there and CI has no Postgres. The rate limit is set high on purpose:
    /// these tests are about what the pipeline serves, and a cold first load of the client is
    /// hundreds of requests (see the PR notes on RateLimiting:PermitLimit).
    /// </summary>
    private sealed class ClientHostingApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");

            // WebApplicationBuilder wires the static web assets manifest only when the
            // environment is Development, and a non-published build has no physical wwwroot —
            // the client's files live scattered across obj/ and bin/ until publish maps them.
            // Without this the whole pipeline is real but every static asset is missing, which
            // would make these tests fail for a reason that does not exist in Development
            // (manifest applied automatically) or in Production (published wwwroot on disk).
            builder.UseStaticWebAssets();

            builder.UseSetting("RateLimiting:PermitLimit", "1000");
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO");

            // Generated at test time, per CLAUDE.md — never written to a configuration file.
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        }
    }
}
