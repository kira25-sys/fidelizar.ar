using Fidelizar.Domain.Exceptions;

namespace Fidelizar.Domain.Entities;

/// <summary>
/// A recorded consent from a <see cref="Miembro"/> (DATA-MODEL §3, I10). Append-only, like the
/// ledger: a withdrawal is a new row with <see cref="Otorgado"/> <c>false</c>, never an update,
/// so the current consent for a <c>(MiembroId, Tipo)</c> pair is simply the newest row.
/// </summary>
public sealed class Consentimiento
{
    public int Id { get; private set; }

    public int NegocioId { get; private set; }

    public int MiembroId { get; private set; }

    public TipoConsentimiento Tipo { get; private set; }

    /// <summary>A withdrawal is a new row with <c>false</c>, not an update to this one.</summary>
    public bool Otorgado { get; private set; }

    /// <summary>Which wording of the consent text the member agreed to.</summary>
    public string VersionTexto { get; private set; } = string.Empty;

    public CanalConsentimiento Canal { get; private set; }

    /// <summary>Who recorded it. Null when self-service, and for every row the phase-0 migration
    /// wrote — those members consented verbally, before user accounts existed.</summary>
    public int? RegistradoPorUsuarioId { get; private set; }

    public DateTime OcurridoEn { get; private set; }

    private Consentimiento()
    {
    }

    /// <summary>
    /// The only way to build a <see cref="Consentimiento"/> row, grant or withdrawal alike —
    /// both are just rows with a different <paramref name="otorgado"/>. Validates
    /// <paramref name="versionTexto"/> is present: recording *that* something was agreed without
    /// recording *which wording* defeats the audit trail this table exists for.
    /// </summary>
    public static Consentimiento Registrar(
        int negocioId,
        int miembroId,
        TipoConsentimiento tipo,
        bool otorgado,
        string versionTexto,
        CanalConsentimiento canal,
        DateTime ocurridoEn,
        int? registradoPorUsuarioId = null)
    {
        if (string.IsNullOrWhiteSpace(versionTexto))
        {
            throw new ValidationException(
                "El consentimiento tiene que registrar qué versión del texto se aceptó (o rechazó).",
                "VERSION_TEXTO_REQUERIDA");
        }

        return new()
        {
            NegocioId = negocioId,
            MiembroId = miembroId,
            Tipo = tipo,
            Otorgado = otorgado,
            VersionTexto = versionTexto,
            Canal = canal,
            RegistradoPorUsuarioId = registradoPorUsuarioId,
            OcurridoEn = ocurridoEn,
        };
    }
}
