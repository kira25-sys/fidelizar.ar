namespace Fidelizar.Shared.Miembros;

/// <summary>One row of S3's alert strip. <c>Tipo</c> travels as the enum's own name (matching
/// the pattern <c>Auth.SesionResponse.Rol</c> already uses for <c>RolUsuario</c>), never as a
/// raw int a client would have to keep a private mapping for.</summary>
public sealed record AlertaMiembro(string Tipo, string Texto);

/// <summary>
/// S3 Ficha del socio — the full counter view (FUNCTIONAL-SPEC §5). Never phone or DNI: that
/// gate is <see cref="FichaCompletaResponse"/> alone (Encargada/Dueño, enforced server-side by
/// the endpoint's own policy, not by this shape).
/// </summary>
public sealed record FichaMostradorResponse(
    int Id,
    string Nombre,
    string? NumeroSocio,
    decimal Saldo,
    DateOnly CorteFecha,
    IReadOnlyList<AlertaMiembro> Alertas);
