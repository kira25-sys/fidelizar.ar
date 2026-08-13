namespace Fidelizar.Domain.Entities;

/// <summary>
/// A loyalty program member (DATA-MODEL §3). The balance never lives here — it is always
/// <c>SUM(Monto)</c> over that member's <see cref="MovimientoCredito"/> rows (I2).
/// </summary>
public sealed class Miembro
{
    public int Id { get; init; }

    public required int NegocioId { get; init; }

    /// <summary>
    /// The POS customer id. Nullable: a member registered at the counter starts unlinked and
    /// <b>accrues nothing until linked</b> — the id is born in the POS, not in the web.
    /// <c>text</c>, not <c>int</c>: other POS systems use non-numeric ids (DATA-MODEL §3, §7).
    /// </summary>
    public string? ClienteExternoId { get; init; }

    /// <summary>Informational number within the club, if the member has one.</summary>
    public string? NumeroSocio { get; init; }

    public required string Nombre { get; init; }

    /// <summary>Accent- and case-folded. Feeds the search (VipNombres, F0-05 — not built here).</summary>
    public required string NombreNormalizado { get; init; }

    /// <summary>Informational. Never an identity key — mixed formats and shared numbers.</summary>
    public string? Telefono { get; init; }

    /// <summary>Informational. Never an identity key — unevenly loaded in the POS.</summary>
    public string? Dni { get; init; }

    /// <summary>Only day and month matter (RN-11); the year is ignored even when present.</summary>
    public DateOnly? FechaNacimiento { get; init; }

    /// <summary>Organisational only (RN-07) — never a calculation boundary.</summary>
    public int? SucursalId { get; init; }

    public required DateOnly FechaAlta { get; init; }

    /// <summary>Deactivates without erasing history.</summary>
    public bool Activo { get; init; } = true;

    public DateTime ActualizadoEn { get; init; }
}
