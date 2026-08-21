using System.Text.Json;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fidelizar.Api.Monitoreo;

/// <summary>
/// The two probes ARCHITECTURE §14's external uptime check consumes, and the body they answer
/// with. See docs/OPERACION-MONITOREO.md.
/// </summary>
public static class OpcionesDeSalud
{
    /// <summary>Checks that only <c>/health/ready</c> runs.</summary>
    public const string TagReady = "ready";

    /// <summary>Name of the readiness check, as it appears in the JSON body.</summary>
    public const string ChequeoBase = "base-de-datos";

    /// <summary>
    /// Liveness: is the process answering HTTP at all? Runs no check, so it stays up while the
    /// database is down — which is what tells the owner apart "the host died" from "Postgres died".
    /// </summary>
    public static HealthCheckOptions Vivo(string instancia) => new()
    {
        Predicate = _ => false,
        ResponseWriter = (context, report) => EscribirAsync(context, report, instancia),
    };

    /// <summary>Readiness: the process is up <em>and</em> the database responds.</summary>
    public static HealthCheckOptions Listo(string instancia) => new()
    {
        Predicate = registration => registration.Tags.Contains(TagReady),
        ResponseWriter = (context, report) => EscribirAsync(context, report, instancia),
    };

    /// <summary>
    /// Status and instance, and nothing else. Both endpoints are anonymous, so the body must never
    /// carry an exception, a stack trace or a connection string (CLAUDE.md).
    /// </summary>
    private static Task EscribirAsync(HttpContext context, HealthReport report, string instancia)
    {
        context.Response.ContentType = "application/json";

        var cuerpo = new
        {
            estado = report.Status.ToString(),
            instancia,
            chequeos = report.Entries.ToDictionary(e => e.Key, e => e.Value.Status.ToString()),
        };

        return context.Response.WriteAsync(JsonSerializer.Serialize(cuerpo));
    }
}
