namespace Fidelizar.Shared.Miembros;

/// <summary>F1-14 — the member as it stands after the link, so the screen confirms the exact id
/// that was recorded rather than echoing what was typed.</summary>
public sealed record VinculacionClienteExternoResponse(
    int MiembroId, string Nombre, string ClienteExternoId, DateTime VinculadoEn);
