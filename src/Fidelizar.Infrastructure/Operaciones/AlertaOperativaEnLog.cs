using Fidelizar.Domain.Operaciones;
using Microsoft.Extensions.Logging;

namespace Fidelizar.Infrastructure.Operaciones;

/// <summary>
/// The default (and, in phase 1, only) <see cref="IAlertaOperativa"/>: one structured
/// <c>Error</c> line, carrying the instance so the owner knows which client is affected
/// (ARCHITECTURE §14). Sending it to a phone waits on the open decision in
/// docs/OPERACION-MONITOREO.md §5 — this is the call site a real sender replaces.
/// </summary>
public sealed class AlertaOperativaEnLog(ILogger<AlertaOperativaEnLog> logger) : IAlertaOperativa
{
    public Task AlertarAsync(string codigo, string detalle, CancellationToken cancellationToken = default)
    {
        logger.LogError("Alerta operativa {CodigoAlerta}: {DetalleAlerta}", codigo, detalle);
        return Task.CompletedTask;
    }
}
