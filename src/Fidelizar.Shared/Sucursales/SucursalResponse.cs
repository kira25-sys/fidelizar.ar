namespace Fidelizar.Shared.Sucursales;

/// <summary>S10 Sucursales (Dueño only).</summary>
public sealed record SucursalResponse(int Id, string Nombre, string? CodigoExterno, bool Activa);
