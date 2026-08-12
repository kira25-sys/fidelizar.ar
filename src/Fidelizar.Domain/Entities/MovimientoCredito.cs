using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Entities;

/// <summary>
/// A single line of the credit ledger — the heart of the product. The table is
/// <b>append-only</b> (I1): a row is never edited and never deleted, in any layer. There is no
/// public setter here that lets a caller change a row after <see cref="Crear"/> constructs it,
/// other than <see cref="FijarSaldoResultante"/>, which only the repository may call, inside the
/// same transaction as the insert (I2, DATA-MODEL §4).
///
/// The constructor is private on purpose: every <see cref="MovimientoCredito"/> in the system
/// goes through <see cref="Crear"/>, so the invariants below (mandatory <c>Motivo</c>, mandatory
/// <c>ConfiguracionId</c> for an accrual, the derived <c>Periodo</c>) cannot be bypassed by
/// constructing the object some other way — and are testable with no database (ARCHITECTURE §3).
/// </summary>
public sealed class MovimientoCredito
{
    public long Id { get; private set; }

    public int NegocioId { get; private set; }

    public int MiembroId { get; private set; }

    /// <summary>When it happened in the real world. A redemption written on paper during a power
    /// cut carries the paper's date — distinct from <see cref="RegistradoEn"/>.</summary>
    public DateOnly FechaEfectiva { get; private set; }

    /// <summary>When the system learned about it. Always "now" (UTC).</summary>
    public DateTime RegistradoEn { get; private set; }

    /// <summary><c>YYYY-MM</c> of <see cref="FechaEfectiva"/>. Derived, never passed in.</summary>
    public string Periodo { get; private set; } = string.Empty;

    public TipoMovimientoCredito Tipo { get; private set; }

    /// <summary>Positive adds, negative subtracts. Always <c>decimal</c> (I4).</summary>
    public decimal Monto { get; private set; }

    /// <summary>The sale's external id. Null for <c>SaldoInicial</c> and <c>Canje</c>.</summary>
    public string? ReferenciaVenta { get; private set; }

    /// <summary>
    /// Who caused it. Null only for <c>sistema</c>. Scalar column on purpose: <c>Usuario</c> is
    /// F1-03 and does not exist yet in this wave, so there is no navigation property and no FK
    /// constraint here. F1-03 introduces <c>Usuario</c> and adds the constraint in a later
    /// migration.
    /// </summary>
    public int? UsuarioId { get; private set; }

    /// <summary>Mandatory for <c>Canje</c>, <c>Ajuste</c>, and any movement with
    /// <see cref="FechaEfectiva"/> earlier than the day it was registered.</summary>
    public string? Motivo { get; private set; }

    /// <summary>
    /// The member's balance right after this movement. Historical evidence only (I2) — never
    /// the source of an answer. Set by the repository inside the same transaction as the insert,
    /// because computing it requires reading the current sum from the database.
    /// </summary>
    public decimal SaldoResultante { get; private set; }

    /// <summary>Which program configuration produced this movement. Mandatory for
    /// <c>Acumulacion</c>; null allowed for the other types.</summary>
    public int? ConfiguracionId { get; private set; }

    private MovimientoCredito()
    {
    }

    /// <summary>
    /// The only way to build a <see cref="MovimientoCredito"/>. Validates the invariants that
    /// DATA-MODEL §4 calls out as easy to get wrong, and derives <see cref="Periodo"/> from
    /// <paramref name="fechaEfectiva"/> so the two can never disagree.
    /// </summary>
    /// <param name="hoy">
    /// Today's date, used only to decide whether this movement is retroactive (and therefore
    /// requires a <paramref name="motivo"/>). Passed in rather than read from the clock so the
    /// rule is testable without a database or a fixed system clock.
    /// </param>
    public static MovimientoCredito Crear(
        int negocioId,
        int miembroId,
        DateOnly fechaEfectiva,
        DateTime registradoEn,
        TipoMovimientoCredito tipo,
        decimal monto,
        DateOnly hoy,
        int? usuarioId = null,
        string? motivo = null,
        int? configuracionId = null,
        string? referenciaVenta = null)
    {
        if (tipo == TipoMovimientoCredito.Acumulacion && configuracionId is null)
        {
            throw new ValidationException(
                "ConfiguracionId es obligatorio en un movimiento de Acumulacion (DATA-MODEL §4).",
                "CONFIGURACION_REQUERIDA");
        }

        var esRetroactivo = fechaEfectiva < hoy;
        var requiereMotivo = tipo is TipoMovimientoCredito.Canje or TipoMovimientoCredito.Ajuste || esRetroactivo;

        if (requiereMotivo && string.IsNullOrWhiteSpace(motivo))
        {
            throw new ValidationException(
                "Motivo es obligatorio para Canje, Ajuste, y para cualquier movimiento retroactivo " +
                "(DATA-MODEL §4).",
                "MOTIVO_REQUERIDO");
        }

        return new MovimientoCredito
        {
            NegocioId = negocioId,
            MiembroId = miembroId,
            FechaEfectiva = fechaEfectiva,
            RegistradoEn = registradoEn,
            Periodo = $"{fechaEfectiva.Year:D4}-{fechaEfectiva.Month:D2}",
            Tipo = tipo,
            Monto = monto,
            ReferenciaVenta = referenciaVenta,
            UsuarioId = usuarioId,
            Motivo = motivo,
            ConfiguracionId = configuracionId,
        };
    }

    /// <summary>
    /// Records the historical balance snapshot (I2). Only the repository implementation calls
    /// this, inside the same transaction as the insert — see <c>MovimientoRepository.Append</c>
    /// in <c>Fidelizar.Infrastructure</c>. Internal so nothing outside that boundary can rewrite
    /// what is supposed to be a fact about the past.
    /// </summary>
    internal void FijarSaldoResultante(decimal saldoResultante) => SaldoResultante = saldoResultante;
}
