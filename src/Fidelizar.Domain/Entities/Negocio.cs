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

    /// <summary>
    /// The business's legal address. Nullable — not every business has it loaded yet — but this
    /// is what resolves the <c>[DOMICILIO]</c> placeholder in the <c>DatosPersonales</c> consent
    /// text (F1-idempotencia-y-alta, 2026-08-19): the wording is fixed, the identifying data is
    /// this business's own, never a literal in code (CLAUDE.md).
    /// </summary>
    public string? Domicilio { get; init; }

    public bool Activo { get; init; } = true;

    public DateTime CreadoEn { get; init; }
}
