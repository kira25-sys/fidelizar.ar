using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Entities;

/// <summary>
/// One audited action (DATA-MODEL §2, Plan §5, R6): support access, and every read of sensitive
/// data. <b>Append-only, like the ledger</b> — there is no public setter beyond what
/// <see cref="Registrar"/> establishes, and
/// <see cref="Fidelizar.Domain.Repositories.IRegistroAuditoriaRepository"/> exposes no Update and
/// no Delete.
///
/// Built ahead of any call site: F1-03 introduces only the entity, its table and its repository.
/// The actions that will actually write here — <c>VerFichaCompleta</c>, <c>AnularMovimiento</c>,
/// and so on — arrive with the features that perform them (F1-08, F1-11, …), the same way F0-09
/// built <see cref="Consentimiento"/> ahead of F1-08's write-blocking rule.
/// </summary>
public sealed class RegistroAuditoria
{
    public long Id { get; private set; }

    public int NegocioId { get; private set; }

    /// <summary>Who did it. Never null — an audited action always has an actor (DATA-MODEL §2).</summary>
    public int UsuarioId { get; private set; }

    /// <summary>E.g. <c>VerFichaCompleta</c>, <c>ExportarDatos</c>, <c>AnularMovimiento</c> (DATA-MODEL §2).</summary>
    public string Accion { get; private set; } = string.Empty;

    public string? EntidadTipo { get; private set; }

    public int? EntidadId { get; private set; }

    /// <summary>
    /// Free-form detail, stored as <c>jsonb</c> (DATA-MODEL §2). A plain <c>string?</c> here on
    /// purpose: Domain has no JSON serialisation concerns (ARCHITECTURE §3) — the caller supplies
    /// already-formed JSON text, or null.
    /// </summary>
    public string? Detalle { get; private set; }

    public DateTime OcurridoEn { get; private set; }

    private RegistroAuditoria()
    {
    }

    public static RegistroAuditoria Registrar(
        int negocioId,
        int usuarioId,
        string accion,
        DateTime ocurridoEn,
        string? entidadTipo = null,
        int? entidadId = null,
        string? detalle = null)
    {
        if (string.IsNullOrWhiteSpace(accion))
        {
            throw new ValidationException(
                "Accion es obligatoria para un registro de auditoria.", "ACCION_REQUERIDA");
        }

        return new RegistroAuditoria
        {
            NegocioId = negocioId,
            UsuarioId = usuarioId,
            Accion = accion,
            EntidadTipo = entidadTipo,
            EntidadId = entidadId,
            Detalle = detalle,
            OcurridoEn = ocurridoEn,
        };
    }
}
