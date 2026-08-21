namespace Fidelizar.Domain.Operaciones;

/// <summary>
/// The seam ARCHITECTURE §14 "alerting to the phone" plugs into. Which service actually sends it
/// is an open decision (docs/OPERACION-MONITOREO.md §5); until the owner takes it, the only
/// implementation writes a structured log line.
/// </summary>
public interface IAlertaOperativa
{
    /// <summary>
    /// Raises an operational alert. <paramref name="detalle"/> describes infrastructure only —
    /// never a member's name, phone, DNI or email (CLAUDE.md); a member is an id.
    /// </summary>
    Task AlertarAsync(string codigo, string detalle, CancellationToken cancellationToken = default);
}
