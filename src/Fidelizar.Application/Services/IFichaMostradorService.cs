namespace Fidelizar.Application.Services;

/// <summary>An alert kind for the S3 counter strip. Phase 1 only ever produces
/// <see cref="Cumpleanos"/> — <see cref="AlergiaODieta"/> needs <c>PerfilMiembro</c> (phase 3,
/// F3-01) and <see cref="ComprasHabituales"/> needs phase-4 aggregation. The unredeemed-balance
/// alert has no row here at all: the hero <c>Saldo</c> figure already satisfies it structurally
/// (FLOW-S2-S5 §2.2).</summary>
public enum TipoAlertaMiembro
{
    Cumpleanos,
    AlergiaODieta,
    ComprasHabituales,
}

public sealed record AlertaMiembroResultado(TipoAlertaMiembro Tipo, string Texto);

/// <summary>S3's full counter view (FUNCTIONAL-SPEC §5) — everything <c>ObtenerSaldo</c> could
/// never carry: name, member number, and the alert strip. Never phone or DNI (that gate belongs
/// to <see cref="IFichaCompletaService"/> alone).</summary>
public sealed record FichaMostradorResultado(
    int Id,
    string Nombre,
    string? NumeroSocio,
    decimal Saldo,
    DateOnly CorteFecha,
    IReadOnlyList<AlertaMiembroResultado> Alertas);

public interface IFichaMostradorService
{
    /// <exception cref="Fidelizar.Domain.Exceptions.EntityNotFoundException">
    /// No member with <paramref name="miembroId"/> exists for this business.
    /// </exception>
    Task<FichaMostradorResultado> ObtenerAsync(
        int negocioId, int miembroId, DateOnly hoy, CancellationToken cancellationToken = default);
}
