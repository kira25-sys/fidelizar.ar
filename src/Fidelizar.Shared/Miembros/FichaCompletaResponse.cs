namespace Fidelizar.Shared.Miembros;

/// <summary>
/// S6 Ficha completa — Encargada/Dueño only (FUNCTIONAL-SPEC §5/§8's privacy split). The one
/// response in this product that carries <c>Telefono</c> and <c>Dni</c>; a <c>Cajero</c> token
/// must never reach the endpoint that returns this shape (enforced server-side by the endpoint's
/// policy, never by this DTO).
/// </summary>
/// <param name="FechaNacimiento">"DD/MM" — only day and month matter (RN-11); carrying a real
/// date would imply a birth year the record does not track.</param>
public sealed record FichaCompletaResponse(
    int Id,
    string Nombre,
    string? NumeroSocio,
    string? ClienteExternoId,
    string? Telefono,
    string? Dni,
    string? FechaNacimiento,
    int? SucursalId,
    bool Activo);
