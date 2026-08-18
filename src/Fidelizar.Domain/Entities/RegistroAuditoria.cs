using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Entities;

/// <summary>
/// One audited action (DATA-MODEL §2): support access, and every read of sensitive data.
/// Append-only, like the ledger — <see cref="Registrar"/> is the only way to build one, and
/// <see cref="Fidelizar.Domain.Repositories.IRegistroAuditoriaRepository"/> exposes no Update and
/// no Delete.
/// </summary>
public sealed class RegistroAuditoria
{
    public long Id { get; private set; }

    public int NegocioId { get; private set; }

    /// <summary>Who did it. Never null — an audited action always has an actor.</summary>
    public int UsuarioId { get; private set; }

    /// <summary>E.g. <c>VerFichaCompleta</c>, <c>ExportarDatos</c>, <c>AnularMovimiento</c>.</summary>
    public string Accion { get; private set; } = string.Empty;

    public string? EntidadTipo { get; private set; }

    public int? EntidadId { get; private set; }

    /// <summary>Free-form detail, stored as <c>jsonb</c>. A plain <c>string?</c> here because
    /// Domain has no JSON concerns (ARCHITECTURE §3): the caller supplies formed JSON, or null.</summary>
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
