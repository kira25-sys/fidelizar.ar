using System.Text.Json;
using Fidelizar.Api.Configurations;
using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Core;

namespace Fidelizar.Api.Tests.Monitoreo;

/// <summary>
/// F1-18, ARCHITECTURE §14 "structured application logs, retained per client". What is checked
/// here is the file that is actually written, not the configuration that was intended: a text
/// template on disk is not a structured log, however structured the call site was.
///
/// <para>
/// Builds its own logger from the real <c>AddSerilogConfiguration</c> and keeps a local reference
/// to it, so another test class booting a <c>WebApplicationFactory</c> in parallel — which
/// replaces the global <c>Log.Logger</c> — cannot make this flaky.
/// </para>
/// </summary>
public class LogsEstructuradosTests : IDisposable
{
    private readonly string _carpeta = Path.Combine(
        Path.GetTempPath(), "fidelizar-logs-" + Guid.NewGuid().ToString("N"));

    private Logger? _logger;

    [Fact]
    public void Cada_linea_del_archivo_es_JSON_valido_y_trae_la_instancia()
    {
        var logger = ConstruirLogger("instancia-de-prueba");

        logger.Information("Linea de prueba {Dato}", 1);

        var lineas = LeerLineas();
        var primera = JsonDocument.Parse(Assert.Single(lineas)).RootElement;

        Assert.Equal(
            "instancia-de-prueba",
            primera.GetProperty("Properties")
                .GetProperty(LoggingConfigurationExtensions.PropiedadInstancia)
                .GetString());
    }

    /// <summary>
    /// CLAUDE.md: nothing personal in a log. S2 searches a member by name, so a request path that
    /// kept its query string would put that name on disk for a month. The name below is invented.
    /// </summary>
    [Fact]
    public void La_ruta_registrada_no_arrastra_el_termino_de_busqueda()
    {
        var logger = ConstruirLogger("instancia-de-prueba");

        // The shape Serilog.AspNetCore writes with IncludeQueryInRequestPath = false: path only.
        logger.Information(
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode}", "GET", "/api/miembros", 200);

        var contenido = string.Join('\n', LeerLineas());

        Assert.Contains("/api/miembros", contenido, StringComparison.Ordinal);
        Assert.DoesNotContain("?q=", contenido, StringComparison.Ordinal);
    }

    /// <summary>
    /// The guard for the line above: flipping this to <c>true</c> would put every member search
    /// term on disk for a month, and it is one word away at all times.
    /// </summary>
    [Fact]
    public void El_request_logging_nunca_incluye_el_query_string()
    {
        var options = new RequestLoggingOptions();

        LoggingConfigurationExtensions.ConfigurarRequestLogging(options);

        Assert.False(options.IncludeQueryInRequestPath);
    }

    private Logger ConstruirLogger(string instancia)
    {
        var configuracion = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Monitoreo:Instancia"] = instancia,
                ["Monitoreo:RutaArchivoLog"] = Path.Combine(_carpeta, "fidelizar-.log"),
            })
            .Build();

        // The real configuration, but an instance of our own: Log.Logger is process-wide and
        // another test class booting a host in parallel replaces it.
        _logger = LoggingConfigurationExtensions.ConstruirConfiguracion(configuracion).CreateLogger();

        return _logger;
    }

    private string[] LeerLineas()
    {
        var archivo = Directory.GetFiles(_carpeta, "fidelizar-*.log").Single();

        // The sink holds the file open with FileShare.ReadWrite (shared: true).
        using var stream = new FileStream(
            archivo, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var lector = new StreamReader(stream);

        return lector.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);

        _logger?.Dispose();

        try
        {
            Directory.Delete(_carpeta, recursive: true);
        }
        catch (IOException)
        {
            // The sink may still hold the file; a leftover temp directory is not a test failure.
        }
    }
}
