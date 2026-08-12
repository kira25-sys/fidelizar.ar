using Serilog;
using Serilog.Events;

namespace Fidelizar.Api.Configurations;

/// <summary>
/// Structured logging, retained per client (ARCHITECTURE §14). Rolling daily file plus console,
/// so a deployment with no shell access still has something an operator can read after the fact.
/// Ported from Dsw2026Tpi (ARCHITECTURE §15), adapted: code-first defaults instead of relying
/// entirely on a "Serilog" section in appsettings.json, so file logging with retention works out
/// of the box even before any per-environment tuning exists. <c>appsettings.json</c> can still
/// override or extend it — <see cref="LoggerConfiguration.ReadFrom"/> is applied last.
/// </summary>
public static class LoggingConfigurationExtensions
{
    private const string LogFilePathTemplate = "Logs/fidelizar-.log";

    // One log file per day, kept for roughly a month — matches the "retained per client" wording
    // of ARCHITECTURE §14 without hard-coding a business number (I8/§6 is about business rules,
    // not ops configuration, but the same discipline applies: no magic number buried in a call
    // site with no explanation).
    private const int RetainedDays = 31;

    public static WebApplicationBuilder AddSerilogConfiguration(this WebApplicationBuilder builder)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .WriteTo.File(
                LogFilePathTemplate,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays,
                shared: true)
            .ReadFrom.Configuration(builder.Configuration)
            .CreateLogger();

        builder.Host.UseSerilog();

        return builder;
    }
}
