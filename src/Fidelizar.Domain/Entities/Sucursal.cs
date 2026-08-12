namespace Fidelizar.Domain.Entities;

/// <summary>
/// A physical branch of a <see cref="Negocio"/>. Organisational only — never a calculation
/// boundary (RN-07). Any query that filters totals by <see cref="Sucursal"/> is a defect
/// (DATA-MODEL §1).
/// </summary>
public sealed class Sucursal
{
    public int Id { get; init; }

    public required int NegocioId { get; init; }

    public required string Nombre { get; init; }

    /// <summary>How the POS names this branch. Used to reject sales carrying an unknown code.</summary>
    public string? CodigoExterno { get; init; }

    public bool Activa { get; init; } = true;
}
