using Fidelizar.Domain.Entities;

namespace Fidelizar.Domain.Consentimientos;

/// <summary>
/// The two consent texts the owner approved 2026-08-19 — both explicitly provisional, to be
/// reviewed again before production (FUNCTIONAL-SPEC §12, README open decision #3, now resolved).
///
/// <para>
/// The boilerplate wording is a constant here on purpose: it is the product's own legal template,
/// identical for every business, not a business-specific number or fact — CLAUDE.md's "no business
/// literal in code" is about <c>Negocio.Nombre</c>, <c>Cuit</c> and <c>Domicilio</c>, which never
/// appear here as literals. <see cref="Resolver"/> is the one place they are substituted in, from
/// the caller's own <see cref="Negocio"/> row, at render time — the template itself carries no
/// business's identity.
/// </para>
///
/// <para>
/// <see cref="Consentimiento.VersionTexto"/> stores only the version tag below, never the full
/// resolved text — exactly the design F1-08 already established: the tag is enough to know which
/// wording a member saw, and the wording itself lives here, not duplicated onto every row.
/// </para>
/// </summary>
public static class TextosConsentimiento
{
    /// <summary>
    /// The asymmetry between the two texts is substantive, not stylistic, and every write path
    /// that touches consent has to respect it: <c>DatosPersonales</c> says alta is impossible
    /// without it — mandatory, alta rejected without a granted consent of this type.
    /// <c>DatosSensibles</c> says explicitly that membership is possible without it and that it is
    /// revocable at any time with no effect on the account — optional, alta accepted without it,
    /// and revoking it never touches <c>Miembro</c> or the ledger (I10 gates future
    /// <c>PerfilMiembro</c> writes, not membership itself).
    /// </summary>
    public const string DatosPersonalesVersion = "DatosPersonales-2026-08-19-v1";

    public const string DatosSensiblesVersion = "DatosSensibles-2026-08-19-v1";

    private const string DatosPersonalesPlantilla =
        "Presto mi consentimiento libre, expreso e informado para que {RazonSocial}, CUIT " +
        "{Cuit}, con domicilio en {Domicilio}, registre y trate mis datos personales (nombre y " +
        "apellido, DNI y teléfono) con la finalidad de darme de alta como socio del VIP Club, " +
        "acumular y canjear mis puntos, identificarme en cualquiera de las sucursales y " +
        "comunicarse conmigo por WhatsApp o teléfono respecto de mi cuenta de socio. Entiendo " +
        "que dar estos datos es voluntario, pero que sin ellos no es posible darme el alta como " +
        "socio.";

    private const string DatosSensiblesPlantilla =
        "Presto mi consentimiento libre, expreso e informado para que {RazonSocial}, CUIT " +
        "{Cuit}, registre y trate datos relativos a mi salud —alergias, intolerancias y " +
        "restricciones alimentarias— y mis preferencias de consumo, con la finalidad de advertir " +
        "al personal antes de recomendarme o venderme un producto que pueda afectarme, y de " +
        "adecuar las recomendaciones a mis gustos. Entiendo que estos son datos sensibles en los " +
        "términos de la Ley 25.326, que darlos es enteramente voluntario, que puedo ser socio " +
        "del VIP Club sin darlos, y que puedo revocar este consentimiento en cualquier momento " +
        "pidiéndolo en cualquier sucursal, sin que ello afecte mi cuenta ni mis puntos.";

    private const string SinCompletar = "(a completar)";

    /// <summary>The resolved <c>DatosPersonales</c> text for <paramref name="negocio"/>, with its
    /// version tag.</summary>
    public static (string VersionTexto, string Texto) DatosPersonalesPara(Negocio negocio) =>
        (DatosPersonalesVersion, Resolver(DatosPersonalesPlantilla, negocio));

    /// <summary>The resolved <c>DatosSensibles</c> text for <paramref name="negocio"/>, with its
    /// version tag.</summary>
    public static (string VersionTexto, string Texto) DatosSensiblesPara(Negocio negocio) =>
        (DatosSensiblesVersion, Resolver(DatosSensiblesPlantilla, negocio));

    private static string Resolver(string plantilla, Negocio negocio) =>
        plantilla
            .Replace("{RazonSocial}", negocio.Nombre, StringComparison.Ordinal)
            .Replace("{Cuit}", negocio.Cuit ?? SinCompletar, StringComparison.Ordinal)
            .Replace("{Domicilio}", negocio.Domicilio ?? SinCompletar, StringComparison.Ordinal);
}
