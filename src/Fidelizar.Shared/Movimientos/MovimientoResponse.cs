namespace Fidelizar.Shared.Movimientos;

/// <summary>S7 Historial de movimientos and S8 Anular movimiento — one ledger row. <c>Tipo</c>
/// travels as the enum's own name, matching every other role/type string this contract already
/// uses. <c>SaldoResultante</c> is historical evidence only (I2) — a client must never re-derive
/// anything from it, the same rule the ledger itself follows.</summary>
public sealed record MovimientoResponse(
    long Id,
    string Tipo,
    decimal Monto,
    DateOnly FechaEfectiva,
    DateTime RegistradoEn,
    string? Motivo,
    decimal SaldoResultante,
    string? UsuarioNombre);
