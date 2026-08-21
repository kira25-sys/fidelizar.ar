using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Entities;

/// <summary>
/// A single line of the credit ledger. The table is append-only (I1): a row is never edited and
/// never deleted, in any layer. The constructor is private so every movement goes through
/// <see cref="Crear"/> and its invariants cannot be bypassed.
/// </summary>
public sealed class MovimientoCredito
{
    public long Id { get; private set; }

    public int NegocioId { get; private set; }

    public int MiembroId { get; private set; }

    /// <summary>When it happened in the real world — a redemption written on paper during a power
    /// cut carries the paper's date. Distinct from <see cref="RegistradoEn"/>.</summary>
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

    /// <summary>Who caused it. Scalar column: the FK to <c>Usuario</c> is added by F1-03's
    /// migration, not here.</summary>
    public int? UsuarioId { get; private set; }

    /// <summary>Mandatory for <c>Canje</c>, <c>Ajuste</c>, and any retroactive movement.</summary>
    public string? Motivo { get; private set; }

    /// <summary>
    /// The member's balance right after this movement. Historical evidence only (I2), never the
    /// source of an answer — the balance is always <c>SUM(Monto)</c>.
    /// </summary>
    public decimal SaldoResultante { get; private set; }

    /// <summary>Which program configuration produced this movement. Mandatory for
    /// <c>Acumulacion</c>.</summary>
    public int? ConfiguracionId { get; private set; }

    /// <summary>
    /// The client-generated key that makes a write retry safe (README decision #6, decided
    /// 2026-08-19; extended to S8 Anular movimiento 2026-08-21). One per attempt: the client keeps
    /// the same key across a lost-response retry, so a second POST with the same key never
    /// produces a second row. Enforced by a unique partial index on
    /// <c>(NegocioId, ClaveIdempotencia)</c> — the guarantee lives in the database, not in a
    /// check-then-insert that a race can slip past. Null for movements no client POSTs
    /// (<c>SaldoInicial</c>, <c>Acumulacion</c>).
    /// </summary>
    public string? ClaveIdempotencia { get; private set; }

    private MovimientoCredito()
    {
    }

    /// <summary>
    /// The only way to build a <see cref="MovimientoCredito"/>. Validates the invariants
    /// DATA-MODEL §4 calls out as easy to get wrong, and derives <see cref="Periodo"/> so the two
    /// can never disagree.
    /// </summary>
    /// <param name="hoy">
    /// Today's date, used only to decide whether this movement is retroactive. Passed in rather
    /// than read from the clock so the rule is testable without a fixed system clock.
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
        string? referenciaVenta = null,
        string? claveIdempotencia = null)
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
            ClaveIdempotencia = claveIdempotencia,
        };
    }

    /// <summary>
    /// Records the historical balance snapshot (I2). Internal so only the repository can call it,
    /// inside the same transaction as the insert.
    /// </summary>
    internal void FijarSaldoResultante(decimal saldoResultante) => SaldoResultante = saldoResultante;
}
