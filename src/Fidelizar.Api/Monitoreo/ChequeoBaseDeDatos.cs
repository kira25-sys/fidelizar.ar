using Fidelizar.Domain.Operaciones;
using Fidelizar.Domain.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Fidelizar.Api.Monitoreo;

/// <summary>
/// The readiness half of ARCHITECTURE §14: the process being alive says nothing about whether it
/// can answer a question about money. Depends on <see cref="IPersistenceProbe"/>, not on a
/// <c>DbContext</c>, so <c>Api</c> still talks to no Infrastructure type (ARCHITECTURE §3).
/// </summary>
public sealed class ChequeoBaseDeDatos(IPersistenceProbe sonda, IAlertaOperativa alerta) : IHealthCheck
{
    /// <summary>Code the alert carries, so a rule can key on it without parsing prose.</summary>
    public const string CodigoAlerta = "BASE_NO_RESPONDE";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (await sonda.RespondeAsync(cancellationToken))
        {
            return HealthCheckResult.Healthy("La base responde.");
        }

        // Fires on every poll while the outage lasts, on purpose: no transition state to get
        // wrong, and a monitor polling once a minute produces one line a minute.
        await alerta.AlertarAsync(
            CodigoAlerta, "La base de datos no responde a la sonda de readiness.", cancellationToken);

        // No exception and no description beyond this: Npgsql's messages can quote the
        // connection string, and /health/ready is anonymous (CLAUDE.md).
        return HealthCheckResult.Unhealthy("La base no responde.");
    }
}
