namespace Fidelizar.Domain.Entities;

/// <summary>
/// How a <see cref="Consentimiento"/> was obtained (DATA-MODEL §3). Persisted as <c>int</c>:
/// values are never reordered and never reused.
/// </summary>
public enum CanalConsentimiento
{
    /// <summary>Recorded at the counter, on the spot.</summary>
    Mostrador = 0,

    /// <summary>The member recorded it themselves (self-service form, later phases).</summary>
    Autogestion = 1,

    /// <summary>
    /// The phase-0 migration's own value (DATA-MODEL §7): the 293 existing members consented
    /// verbally, with no digital form to record it against. Weaker evidence than Law 25.326
    /// prefers, and superseded by any later explicit consent row.
    /// </summary>
    MigracionVerbal = 2,
}
