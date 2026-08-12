namespace Fidelizar.Domain.Entities;

/// <summary>
/// The numbers that were <c>const</c> in Octaviano (DATA-MODEL §1, Plan §6). <b>Versioned,
/// never overwritten</b>: a configuration row is closed (<see cref="VigenteHasta"/> set) and a
/// new one opened whenever the program changes, so a balance accumulated under an old rule stays
/// explainable. That versioning is an <c>Application</c>-layer operation (F2-05) — this entity
/// only carries the shape.
/// </summary>
public sealed class ConfiguracionPrograma
{
    public int Id { get; init; }

    public required int NegocioId { get; init; }

    /// <summary>RN-01. <c>numeric(5,4)</c> — <c>0.0300m</c> is 3%.</summary>
    public required decimal PorcentajeAcumulacion { get; init; }

    /// <summary>RN-06. Null means the program has no monthly target at all.</summary>
    public decimal? ObjetivoMensual { get; init; }

    /// <summary>RN-16..RN-23. Off by default for a new business.</summary>
    public bool GraciaHabilitada { get; init; }

    /// <summary>RN-16. Default 3 when grace is enabled.</summary>
    public int? MesesDeGracia { get; init; }

    /// <summary>RN-17.</summary>
    public decimal? UmbralMesMalo { get; init; }

    /// <summary>RN-23. Default 3 when grace is enabled (decided 2026-08-12).</summary>
    public int? TopeMesesCongelados { get; init; }

    public required DateOnly VigenteDesde { get; init; }

    /// <summary>Null means this is the current configuration for the business.</summary>
    public DateOnly? VigenteHasta { get; init; }

    /// <summary>
    /// Who created this configuration version. Scalar column on purpose: <c>Usuario</c> is
    /// F1-03 and does not exist yet in this wave, so there is no navigation property and no FK
    /// constraint here. F1-03 introduces <c>Usuario</c> and adds the constraint in a later
    /// migration.
    /// </summary>
    public required int CreadoPorUsuarioId { get; init; }
}
