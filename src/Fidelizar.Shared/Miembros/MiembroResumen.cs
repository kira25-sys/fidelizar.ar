namespace Fidelizar.Shared.Miembros;

/// <summary>
/// S2 Buscar socio — one result row: name, member number, balance, and the cutoff date every
/// balance must carry (ARCHITECTURE §13 R3 — "not in any screen"). <c>FechaAlta</c> is the field
/// FLOW-S2-S5.md §1.4 uses to disambiguate a homonym group: the one value in this row that is
/// never null, unlike <c>NumeroSocio</c>.
/// </summary>
public sealed record MiembroResumen(
    int Id, string Nombre, string? NumeroSocio, decimal Saldo, DateOnly FechaAlta, DateOnly CorteFecha);
