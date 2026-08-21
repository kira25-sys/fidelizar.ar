namespace Fidelizar.Application.Services;

/// <summary>One row of the "socios sin vincular" work queue. No phone, no DNI: linking a POS id
/// never needs them, and nothing personal leaves the server that the job does not require.</summary>
public sealed record MiembroSinVincularResultado(
    int Id, string Nombre, string? NumeroSocio, DateOnly FechaAlta, int? SucursalId);

/// <summary>
/// What the caller supplies to link a member (ROADMAP F1-14). Distinct from
/// <c>Fidelizar.Shared.Miembros.VincularClienteExternoRequest</c> — <c>Fidelizar.Application</c>
/// cannot reference <c>Fidelizar.Shared</c> (ARCHITECTURE §3).
/// </summary>
/// <param name="NegocioId">Required, not a convention (I8).</param>
/// <param name="MiembroId">The member being linked.</param>
/// <param name="ClienteExternoId">The POS customer id. Trimmed; blank or absent is rejected.</param>
/// <param name="UsuarioId">Who is linking. Recorded in <c>RegistroAuditoria</c>.</param>
public sealed record VincularClienteExternoSolicitud(
    int NegocioId, int MiembroId, string? ClienteExternoId, int UsuarioId);

/// <summary>The member as it stands after the link.</summary>
public sealed record VinculacionResultado(
    int MiembroId, string Nombre, string ClienteExternoId, DateTime VinculadoEn);

/// <summary>
/// F1-14 "Socios sin vincular": a member registered at the counter (S5) has no
/// <c>ClienteExternoId</c> and accrues nothing until <c>Encargada</c>/<c>Dueño</c> links one
/// (DATA-MODEL §3) — this is the use case that stops that from happening silently. Knows nothing
/// about HTTP or EF (ARCHITECTURE §3).
/// </summary>
public interface IVinculacionMiembroService
{
    /// <summary>
    /// The business's members with no <c>ClienteExternoId</c>, oldest registration first. Not the
    /// "all members" listing FUNCTIONAL-SPEC §4 forbids: it excludes every linked member by
    /// construction and empties as the work gets done.
    /// </summary>
    Task<IReadOnlyList<MiembroSinVincularResultado>> ListarSinVincularAsync(
        int negocioId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Links one member to one POS id, and audits who did it
    /// (<c>Accion = "VincularClienteExterno"</c>, DATA-MODEL §2) in the same transaction.
    /// </summary>
    /// <exception cref="Fidelizar.Domain.Exceptions.ValidationException">
    /// Blank <c>ClienteExternoId</c> (<c>CLIENTE_EXTERNO_ID_REQUERIDO</c>).
    /// </exception>
    /// <exception cref="Fidelizar.Domain.Exceptions.EntityNotFoundException">
    /// No such member in this business — including one that exists under another
    /// <c>NegocioId</c>, which answers identically so the response never reveals that it exists
    /// somewhere else (I8).
    /// </exception>
    /// <exception cref="Fidelizar.Domain.Exceptions.ConflictException">
    /// The member is already linked (<c>MIEMBRO_YA_VINCULADO</c>), or the id is already linked to
    /// another member of this business (<c>CLIENTE_EXTERNO_ID_DUPLICADO</c> — the same code S5
    /// alta already uses).
    /// </exception>
    Task<VinculacionResultado> VincularAsync(
        VincularClienteExternoSolicitud solicitud, CancellationToken cancellationToken = default);
}
