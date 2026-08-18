namespace Fidelizar.Shared.Auth;

/// <summary>
/// S1 — what login and "quién soy" return: the signed-in staff member's own identity, never a
/// member's (no privacy concern here — this is the cashier's own name and role, ARCHITECTURE §8).
/// </summary>
public sealed record SesionResponse(int Id, string NombreCompleto, string Rol, int NegocioId, int? SucursalId);
