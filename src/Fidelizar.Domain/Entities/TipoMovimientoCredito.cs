namespace Fidelizar.Domain.Entities;

/// <summary>
/// What originated a ledger movement. Persisted as <c>int</c> (DATA-MODEL §4) — values are
/// <b>never reordered and never reused</b>. Append only.
/// </summary>
public enum TipoMovimientoCredito
{
    /// <summary>The balance a member already had (e.g. in a migrated spreadsheet) at cutoff time.</summary>
    SaldoInicial = 0,

    /// <summary>The accrual percentage of a paid sale. Always carries <c>ConfiguracionId</c>.</summary>
    Acumulacion = 1,

    /// <summary>A member redeemed credit. Always carries a mandatory <c>Motivo</c>.</summary>
    Canje = 2,

    /// <summary>
    /// A correction over a movement that already exists. The only way to correct anything — a
    /// movement is never edited (I1, I3). Always carries a mandatory <c>Motivo</c>.
    /// </summary>
    Ajuste = 3,
}
