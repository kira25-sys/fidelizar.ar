using System.Net;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Fidelizar.Api.Tests;

/// <summary>
/// Arrancar el cliente WebAssembly descarga ~200 archivos de una, así que con el limitador global
/// aplicado a los estáticos una sola primera carga se pasa del presupuesto y el browser recibe 429
/// en la mayor parte de <c>_framework/</c>: la aplicación no arranca. Pasó el 2026-08-21 contra la
/// base de desarrollo.
///
/// <para><b>Por qué no lo cazó ningún test antes:</b> el arnés de <c>ClientHostingPipelineTests</c>
/// sube <c>PermitLimit</c> a 1000, así que el límite real nunca se alcanzaba. Estos tests usan un
/// límite deliberadamente chico para que el fallo sea observable.</para>
/// </summary>
public class EstaticosSinLimiteDeTasaTests
{
    private const int LimiteChico = 5;

    /// <summary>El caso real: muchos más pedidos que el límite, ninguno puede volver 429.</summary>
    [Fact]
    public async Task Cargar_el_cliente_no_agota_el_limitador()
    {
        using var factory = new ApiConLimiteChico();
        var client = factory.CreateClient();

        var respuestas = new List<HttpStatusCode>();
        for (var i = 0; i < LimiteChico * 4; i++)
        {
            var respuesta = await client.GetAsync("/_framework/blazor.webassembly.js");
            respuestas.Add(respuesta.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, respuestas);
    }

    /// <summary>Lo mismo para el index y para una ruta del SPA, que salen por el fallback.</summary>
    [Theory]
    [InlineData("/")]
    [InlineData("/ingreso")]
    public async Task Recargar_una_pantalla_no_agota_el_limitador(string ruta)
    {
        using var factory = new ApiConLimiteChico();
        var client = factory.CreateClient();

        var respuestas = new List<HttpStatusCode>();
        for (var i = 0; i < LimiteChico * 4; i++)
        {
            var respuesta = await client.GetAsync(ruta);
            respuestas.Add(respuesta.StatusCode);
        }

        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, respuestas);
    }

    /// <summary>
    /// El control que hace que los dos de arriba signifiquen algo: la API sí se sigue limitando.
    /// Sin esto, un limitador desactivado por error pasaría por "estáticos exentos".
    /// </summary>
    [Fact]
    public async Task La_API_sigue_limitada()
    {
        using var factory = new ApiConLimiteChico();
        var client = factory.CreateClient();

        var respuestas = new List<HttpStatusCode>();
        for (var i = 0; i < LimiteChico * 4; i++)
        {
            var respuesta = await client.GetAsync("/api/no-existe");
            respuestas.Add(respuesta.StatusCode);
        }

        Assert.Contains(HttpStatusCode.TooManyRequests, respuestas);
    }

    private sealed class ApiConLimiteChico : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.UseStaticWebAssets();

            builder.UseSetting("RateLimiting:PermitLimit", LimiteChico.ToString());
            builder.UseSetting("RateLimiting:WindowSeconds", "60");
            builder.UseSetting(
                "ConnectionStrings:DefaultConnection",
                "Host=localhost;Database=CAMBIAR_ESTO;Username=CAMBIAR_ESTO;Password=CAMBIAR_ESTO");

            // Generada en el test, nunca escrita a un archivo de configuración (CLAUDE.md).
            builder.UseSetting("Jwt:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
        }
    }
}
