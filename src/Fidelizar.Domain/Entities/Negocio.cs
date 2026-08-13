namespace Fidelizar.Domain.Entities;

/// <summary>
/// The client business. In a single-tenant deployment there is exactly one row, and it still
/// exists — everything else references it (DATA-MODEL §1).
/// </summary>
public sealed class Negocio
{
    public int Id { get; init; }

    public required string Nombre { get; init; }

    public string? Cuit { get; init; }

    public bool Activo { get; init; } = true;

    public DateTime CreadoEn { get; init; }
}
