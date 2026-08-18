namespace Fidelizar.Domain.Entities;

/// <summary>
/// What originated a ledger movement (DATA-MODEL §4). Persisted as <c>int</c>: values are never
/// reordered and never reused.
/// </summary>
public enum TipoMovimientoCredito
{
    /// <summary>The balance a member already had at cutoff time.</summary>
    SaldoInicial = 0,

    /// <summary>The accrual percentage of a paid sale. Always carries <c>ConfiguracionId</c>.</summary>
    Acumulacion = 1,

    /// <summary>A member redeemed credit. Always carries a mandatory <c>Motivo</c>.</summary>
    Canje = 2,

    /// <summary>The only way to correct anything — a movement is never edited (I1, I3). Always
    /// carries a mandatory <c>Motivo</c>.</summary>
    Ajuste = 3,
}
