namespace Fidelizar.Shared.Miembros;

/// <summary>
/// F1-14 "Socios sin vincular" — one member registered at the counter that still has no
/// <c>ClienteExternoId</c>, and therefore accrues nothing (DATA-MODEL §3). No phone and no DNI:
/// linking a POS id does not need them, and <c>Shared</c> is compiled into the browser.
/// <c>FechaAlta</c> is what tells the Encargada how long the member has been waiting.
/// </summary>
public sealed record MiembroSinVincularResponse(
    int Id, string Nombre, string? NumeroSocio, DateOnly FechaAlta, int? SucursalId);
