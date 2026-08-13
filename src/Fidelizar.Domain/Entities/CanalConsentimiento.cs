namespace Fidelizar.Domain.Entities;

/// <summary>
/// How a <see cref="Consentimiento"/> was obtained (DATA-MODEL §3). Persisted as <c>int</c> —
/// values are <b>never reordered and never reused</b>.
/// </summary>
public enum CanalConsentimiento
{
    /// <summary>Recorded at the counter, on the spot.</summary>
    Mostrador = 0,

    /// <summary>The member recorded it themselves (self-service form, later phases).</summary>
    Autogestion = 1,

    /// <summary>
    /// The phase-0 migration's own value (DATA-MODEL §7): the 293 existing members consented
    /// verbally when they joined Octaviano's VIP club — there was no digital form to record it
    /// against. Weaker evidence than Law 25.326 prefers for health data; any member passing
    /// through the counter can be re-consented explicitly, which supersedes this row (consent is
    /// append-only, so the newer row simply outranks it).
    /// </summary>
    MigracionVerbal = 2,
}
