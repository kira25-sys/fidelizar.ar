using Fidelizar.Api.Monitoreo;
using Fidelizar.Api.Options;
using Serilog;
using Serilog.AspNetCore;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace Fidelizar.Api.Configurations;

/// <summary>
/// Structured logging, retained per client (ARCHITECTURE §14). Human-readable console for
/// whoever is watching a terminal; JSON on disk, because "structured" has to survive being
/// written to a file if anything is ever to query it. Ported from Dsw2026Tpi (ARCHITECTURE §15),
/// adapted: retention and instance identity come from <see cref="MonitoreoSettings"/> instead of
/// being constants, so one deployment per client can differ without a rebuild.
/// <c>appsettings.json</c> can still override or extend it — <c>ReadFrom.Configuration</c> is
/// applied last.
/// </summary>
public static class LoggingConfigurationExtensions
{
    /// <summary>Instance identity, stamped on every line so one log store can hold several.</summary>
    public const string PropiedadInstancia = "Instancia";

    /// <summary>
    /// Options for the one request line Serilog writes per request. S2 searches a member by name,
    /// so <c>/api/miembros?q=...</c> carries a person's name: the query string never joins the
    /// logged path (CLAUDE.md). It is Serilog's default, and it is set explicitly here so that
    /// turning it on is a visible decision rather than an omission.
    /// </summary>
    public static void ConfigurarRequestLogging(RequestLoggingOptions options) =>
        options.IncludeQueryInRequestPath = false;

    public static WebApplicationBuilder AddSerilogConfiguration(this WebApplicationBuilder builder)
    {
        Log.Logger = ConstruirConfiguracion(builder.Configuration).CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }

    /// <summary>
    /// The logger configuration itself, separate from assigning it to the global
    /// <c>Log.Logger</c>, so a test can build the real thing and hold its own instance —
    /// <c>Log.Logger</c> is process-wide and belongs to whichever host booted last.
    /// </summary>
    public static LoggerConfiguration ConstruirConfiguracion(IConfiguration configuration)
    {
        var monitoreo = configuration
            .GetSection(MonitoreoSettings.SeccionConfiguracion)
            .Get<MonitoreoSettings>() ?? new MonitoreoSettings();

        return new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithProperty(PropiedadInstancia, monitoreo.Instancia)
            // I8: every line attributable to a business. A plain HttpContextAccessor works here
            // even though this runs before the container exists — the accessor keeps the current
            // context in a static AsyncLocal, which ASP.NET Core populates per request as long as
            // AddHttpContextAccessor() is registered (it is, in Program.cs). Being an enricher
            // rather than middleware also means it applies to lines written from anywhere in the
            // pipeline, including ExceptionHandlingMiddleware, which sits above authentication.
            .Enrich.With(new EnriquecedorDeNegocio(new HttpContextAccessor()))
            .WriteTo.Console()
            .WriteTo.File(
                // JsonFormatter ships inside Serilog itself — no extra package (CLAUDE.md).
                new JsonFormatter(renderMessage: true),
                monitoreo.RutaArchivoLog,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: monitoreo.RetencionDias,
                fileSizeLimitBytes: monitoreo.TamanoMaximoArchivoBytes,
                rollOnFileSizeLimit: true,
                shared: true)
            .ReadFrom.Configuration(configuration);
    }
}
