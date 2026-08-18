namespace Fidelizar.Shared.Miembros;

/// <summary>
/// S3 Ficha del socio — the balance piece only (ARCHITECTURE §13 R3: never render a balance
/// without its cutoff date next to it). <c>CorteFecha</c> is the program's declared cutoff
/// (F0-07) — the closest thing to a freshness date until phase 2's <c>LoteImportacion</c>
/// exists; see docs/REST-CONTRACT-F1.md for why the rest of S3 (name, alerts) is not here yet.
/// </summary>
public sealed record SaldoMiembroResponse(int MiembroId, decimal Saldo, DateOnly CorteFecha);
