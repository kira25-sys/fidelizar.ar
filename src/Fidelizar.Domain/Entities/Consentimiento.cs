namespace Fidelizar.Domain.Entities;

/// <summary>
/// A recorded consent from a <see cref="Miembro"/> (DATA-MODEL §3, I10, Plan §7). Built from
/// phase 1, never retrofitted — F0-09 advances only this entity and its table, ahead of the rest
/// of F1-08 (the consent service and the write-blocking rule), so the 293 migrated members are
/// never loaded with no consent record at all.
///
/// <para>
/// <b>Append-only, like the ledger.</b> A withdrawal is a new row with <see cref="Otorgado"/>
/// <c>false</c>, never an update to an existing one — the current consent for a
/// <c>(MiembroId, Tipo)</c> pair is simply the newest row. There is no public setter here beyond
/// what <see cref="Registrar"/> establishes at construction, and no repository method exists yet
/// to change one (that belongs to F1-08's consent service).
/// </para>
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

    /// <summary>
    /// Who recorded it. Null when self-service, and also null for every row F0-09 writes: the
    /// 293 migrated members consented verbally, in person, before this product's user accounts
    /// existed — there is no <c>Usuario</c> to stamp (<c>Usuario</c> is F1-03 and does not exist
    /// yet in this wave either). Scalar column on purpose, no navigation, no FK constraint, same
    /// as <c>MovimientoCredito.UsuarioId</c> and <c>Corte.DeclaradoPorUsuarioId</c>.
    /// </summary>
    public int? RegistradoPorUsuarioId { get; private set; }

    public DateTime OcurridoEn { get; private set; }

    private Consentimiento()
    {
    }

    public static Consentimiento Registrar(
        int negocioId,
        int miembroId,
        TipoConsentimiento tipo,
        bool otorgado,
        string versionTexto,
        CanalConsentimiento canal,
        DateTime ocurridoEn,
        int? registradoPorUsuarioId = null) =>
        new()
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
